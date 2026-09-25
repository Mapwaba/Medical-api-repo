namespace LandaDoc.Payment.Services;

public class MokoAfrikaOptions
{
    public string BaseUrl { get; set; } = "";
    public string MerchantId { get; set; } = "";
    public string MerchantSecret { get; set; } = "";
    public string HmacKey { get; set; } = "";
    public string AesKey { get; set; } = "";
    public string CallbackBaseUrl { get; set; } = "";
}
