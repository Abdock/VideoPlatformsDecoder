namespace VideoPlatform.YouTube;

internal class SwapOperation : ISignatureModifyOperation
{
    public string ModifySignature(string signature, int argument)
    {
        var signatureChars = signature.ToCharArray();
        const int i = 0;
        var j = argument % signatureChars.Length;
        (signatureChars[i], signatureChars[j]) = (signatureChars[j], signatureChars[i]);
        var modifiedSignature = string.Join("", signatureChars);
        return modifiedSignature;
    }
}