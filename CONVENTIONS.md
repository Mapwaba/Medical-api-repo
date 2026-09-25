# Service conventions

These are the patterns `Identity` and `Appointment` follow. Use them as the worked example when
building out the remaining services (Admin, Availability, Document, Notification, Payment,
Review, Search), the Gateway, and the frontends — don't invent a different shape per service.

## Layering

```
Controller (thin)  →  I*Service / *Service (business logic)  →  DbContext
```

- Controllers validate the request (`ModelState.IsValid`) and translate a service result into an
  `IActionResult`. They do not talk to `DbContext` or `IPublishEndpoint` directly.
  See `Services/Appointment/Controllers/AppointmentsController.cs`.
- Business logic — persistence, conflict/invariant checks, event publishing — lives in a service
  behind an interface, e.g. `IAuthService`/`AuthService`, `IAppointmentService`/`AppointmentService`.
  A service method returns a plain result type (e.g. `AuthResult`, `CreateAppointmentResult`) with
  a status enum — never an `IActionResult` — so it stays framework-agnostic and testable without
  spinning up MVC.
- Don't add a repository layer over `DbContext`. EF Core's `DbContext` already *is* the
  data-access/unit-of-work abstraction; wrapping it in `IRepository<T>` is a redundant layer for
  services this size.

## Dependency injection

- Register an interface for anything with business logic or a side effect (services, token
  issuance, event publishing): `builder.Services.AddScoped<IAppointmentService, AppointmentService>();`
- Register the DbContext concretely (`AddDbContext<TContext>`) — there's no abstraction to gain
  there.
- Stateless helpers (password hashing) can be `Singleton`; anything using the `DbContext` or
  `IPublishEndpoint` must be `Scoped`.

## Validation

Every controller action starts with:
```csharp
if (!ModelState.IsValid) return ValidationProblem(ModelState);
```
Put the actual rules on the DTO with DataAnnotations (`[Required]`, `[EmailAddress]`,
`[MinLength]`/`[MaxLength]`) in `LandaDoc.Shared/DTOs`, not in the controller.

## AuthN/AuthZ

- JWT bearer auth, configured identically in every service from `Jwt:Secret` / `Jwt:Issuer`
  config keys (see `Program.cs` in either service) — same signing key across services so a token
  issued by Identity validates everywhere else.
- Protect endpoints with `[Authorize(Roles = "...")]`.
- **Never trust a client-supplied id for "who is this."** Always read the caller's identity from
  the JWT claim: `Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)`. Appointment does
  this for `patientId` — follow the same pattern anywhere an endpoint acts "on behalf of" the
  caller.
- **Acting on behalf of someone else is a narrow, explicit exception to the rule above — not a
  loophole in it.** The caller's own identity still comes exclusively from the JWT; a
  client-supplied *target* id is allowed only as who the action is *for*, and only after it's
  checked against a server-side authorization record that itself was never populated from client
  input. See `CreateAppointmentRequest.BookForPatientId` / `AppointmentService.CreateAsync`
  (`Services/Appointment/Services/AppointmentService.cs`): the target is checked against
  `booking_authorizations`, a local table Appointment maintains purely by consuming Identity's
  `FamilyLinkAcceptedEvent` / `DependentAddedEvent` (and removes rows on the corresponding
  `*Revoked`/`*Removed` events) — never written from a request body. Reuse this shape for the
  next "on behalf of" feature rather than trusting a target id directly.

## Secrets

- Local dev: `dotnet user-secrets set "Jwt:Secret" "..."` / `"ConnectionStrings:Conx"` — run
  `dotnet user-secrets init` in the service directory first if `UserSecretsId` isn't in the
  csproj yet.
- Every other environment: environment variables (`Jwt__Secret`, `ConnectionStrings__Conx`) —
  ASP.NET Core's config layering picks these up automatically, no code change needed.
- Committed `appsettings.json` should never hold a real secret value — leave the key present
  with an empty string (documents what config the service expects) and let user-secrets/env vars
  fill it in.

## Cross-service events

- MassTransit `IPublishEndpoint` to publish, `IConsumer<T>` to consume.
- Event record types live in `LandaDoc.Shared/Events/`, **not** in the publishing service's own
  project — see `BookingCreatedEvent`, `PaymentFailedEvent`, `UserRegisteredEvent`. Every service
  already references `LandaDoc.Shared`, so any service can consume any event without taking a
  project reference on the service that publishes it. (We initially put these in each publisher's
  own `Events/` folder; that broke the first time a second service — Availability, consuming
  `BookingCreatedEvent` — needed to reference one, since it would have meant a direct project
  reference from Availability to Appointment. Moved them to Shared to fix it.)
- A consumer is its own file in the *consuming* service's `Consumers/` folder (e.g.
  `Availability/Consumers/BookingCreatedConsumer.cs`), depending on the consuming service's
  `I*Service` interface — not nested inside another class.
- Publish *after* the triggering DB write commits, not before.

## Error handling

`builder.Services.AddProblemDetails();` plus, outside `Development`, `app.UseExceptionHandler();`
and `app.UseHsts();` — so an unhandled exception returns a safe `application/problem+json` body
instead of leaking a stack trace. Development still gets the full diagnostic page.

## Rate limiting

Any public, unauthenticated, write-ish endpoint (login, register, password reset, ...) should sit
behind `Microsoft.AspNetCore.RateLimiting` (`AddRateLimiter` + `.RequireRateLimiting("name")`) —
see Identity's `Program.cs` for the fixed-window limiter on `/api/auth/*`. It's built into the
framework, no extra package.

## Naming gotcha

Don't name a class the same as a namespace segment that matches the project's *root* namespace.
`LandaDoc.Appointment` is both the root namespace and, in `Models/Appointment.cs`, a class name —
inside any namespace nested under `LandaDoc.Appointment` (e.g. `LandaDoc.Appointment.Data`), a
bare `Appointment` resolves to the *namespace*, not the class, and fails with "namespace used
like a type." Qualify it (`Models.Appointment`) instead of importing it with a bare `using`.
