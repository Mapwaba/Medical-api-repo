namespace LandaDoc.Payment.Tests;

// Replies and callbacks copied from FreshPay's PayDRC API document (sections 6.4 to 8.4), so the
// mobile money module is checked against what FreshPay says it sends before sandbox access.
public static class FreshPaySamples
{
    // 6.4: the first reply to a debit or credit request (an acknowledgement, not the outcome)
    public const string RequestReceived = """
        {
          "Amount": 100,
          "Comment": "Transaction Received Successfully",
          "Created_At": "2024-03-08 08:08:51.533410",
          "Currency": "CDF",
          "Customer_Number": "972148867",
          "Reference": "testfp09",
          "Status": "Success",
          "Transaction_id": "PDABXkT03IfD08M9PfR24Ci4",
          "Updated_At": "2024-03-08 08:08:51.533410"
        }
        """;

    // 6.5: a refused request
    public const string RequestRefused = """
        {
          "Comment": "Customer number is incorrect, be sure to start with 243",
          "Status": "Error",
          "resultCode": 1,
          "resultCodeDescription": "request not executed...",
          "resultCodeError": 405,
          "resultCodeErrorDescription": "Customer number is incorrect, be sure to start with 243..."
        }
        """;

    // 6.6: callback for a debit the customer didn't complete
    public const string DebitFailedCallback = """
        {
          "Action": "debit",
          "Amount": 100.0,
          "Comment": "Transaction Found",
          "Currency": "CDF",
          "Customer_Details": "972148867",
          "Financial_Institution_id": "",
          "Method": "airtel",
          "PayDRC_Reference": "PD9pLLM03QZJ08X7fXM242x6",
          "Reference": "testfp09",
          "Status": "Success",
          "Status_Description": "La reference de la transaction est invalide, veuillez reesayez ou contactez le service client au 1213",
          "Trans_Status": "Failed",
          "Trans_Status_Description": "La reference de la transaction est invalide, veuillez reesayez ou contactez le service client au 1213"
        }
        """;

    // 6.6: callback for a successful credit (a payout); the merchant balance in the message is left out
    public const string CreditSucceededCallback = """
        {
          "Action": "credit",
          "Amount": 100.0,
          "Comment": "Transaction Found",
          "Currency": "CDF",
          "Customer_Details": "972148867",
          "Financial_Institution_id": "CI240308.0908.C64554",
          "Method": "airtel",
          "PayDRC_Reference": "PDABXkT03IfD08M9PfR24Ci4",
          "Reference": "testfp09",
          "Status": "Success",
          "Status_Description": "Trans.ID: CI240308.0908.C64554 vous avez envoye de 100.0000 CDF a 972148867.",
          "Trans_Status": "Success",
          "Trans_Status_Description": "Transaction successful"
        }
        """;

    // 8.4: the verify action's reply
    public const string VerifyFound = """
        {
          "Action": "debit",
          "Amount": 100.0,
          "Comment": "Transaction Found",
          "Created_at": "2024-03-08 08:05:37",
          "Currency": "CDF",
          "Customer_Details": "972148867",
          "Financial_Institution_id": "null",
          "Method": "airtel",
          "Reference": "testfp09",
          "Status": "Success",
          "Trans_Status": "Failed",
          "Trans_Status_Description": "La reference de la transaction est invalide, veuillez reesayez ou contactez le service client au 1213",
          "Transaction_id": "PD9pLLM03QZJ08X7fXM242x6",
          "Updated_at": "2024-03-08 08:51:30"
        }
        """;

    // Moko Afrika developer portal (sandbox.gofreshpay.com): the callback payload of a paid debit.
    // Note "Successful", where the PDF says "Success".
    public const string PortalPaidCallback = """
        {
            "Status": "Success",
            "Comment": "Transaction Found",
            "Trans_Status": "Successful",
            "Currency": "CDF",
            "Amount": 5000.0,
            "Method": "mpesa",
            "Customer_Details": "243970000000",
            "Reference": "order_001",
            "PayDRC_Reference": "PDxK3mN09vR2qL7y26wPz",
            "Action": "debit",
            "Status_Description": "Transaction successful",
            "Trans_Status_Description": "Paiement recu avec succes",
            "Financial_Institution_id": "MP260405.1234.A56789"
        }
        """;

    // The portal's sandbox "Pending → Success" numbers answer the request with a pending
    // acknowledgement (shape assumed from the portal's other replies)
    public const string RequestPending = """
        {
          "Status": "Pending",
          "Comment": "Transaction Received Successfully",
          "Reference": "test_001",
          "Customer_Number": "243810000003",
          "Transaction_id": "PDxK3mN09vR2qL7y26wPz"
        }
        """;

    // What the sandbox gateway itself answered to an empty request (HTTP 400)
    public const string RequestMalformed = """{"detail":"merchant_id is required"}""";

    // 9: "Transaction Identifier Not Recognized", in the shape of 6.5
    public const string VerifyNotFound = """
        {
          "Comment": "The transaction identifier is not recognized in the system.",
          "Status": "Error",
          "resultCode": 1,
          "resultCodeError": 404,
          "resultCodeErrorDescription": "The provided transaction identifier does not exist in the system."
        }
        """;
}
