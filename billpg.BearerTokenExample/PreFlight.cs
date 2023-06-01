internal class PreFlight
{
    public static readonly string DedicationText = GenerateDedicationText();

    private static string GenerateDedicationText()
    {
        const string Line1 = "Dedicated to my Treacle.";
        const string Line2 = "Lots of love, Bill.";
        const int DistanceToTheMoonAndBackInMiles = 238855 * 2;

        var rootBytes = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                System.Text.Encoding.ASCII.GetBytes(Line1),
                System.Text.Encoding.ASCII.GetBytes(Line2),
                DistanceToTheMoonAndBackInMiles,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                96 * 3 / 8);

        return Convert.ToBase64String(rootBytes);
    }
}