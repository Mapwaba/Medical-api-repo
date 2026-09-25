namespace LandaDoc.Shared.Models;

public enum UserRole { Patient, Doctor, Admin }
public enum AppointmentStatus { Pending, Confirmed, Completed, Cancelled, Rescheduled }
public enum PaymentStatus { Pending, Completed, Refunded, Failed }
public enum PaymentProvider { Stripe, MokoAfrika }
public enum MobileMoneyOperator { Airtel, Orange, Mpesa, Africell }
public enum DayOfWeekEnum { Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday }
public enum Gender { Male, Female, Other }
public enum ClinicType { PrivatePractice, Clinic, Hospital, Lab }
public enum DoctorApprovalStatus { Pending, Approved, Suspended, Rejected }
public enum FamilyRelationType { Parent, Sibling }
public enum FamilyLinkStatus { Pending, Accepted, Declined, Revoked }