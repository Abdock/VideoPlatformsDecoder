namespace VideoPlatform.YouTube;

internal class SwapOperation : ISignatureModifyOperation
{
    public int Index { get; set; }

    public string ModifySignature(string signature)
    {
        var signatureChars = signature.ToCharArray();
        const int i = 0;
        var j = Index % signatureChars.Length;
        (signatureChars[i], signatureChars[j]) = (signatureChars[j], signatureChars[i]);
        var modifiedSignature = string.Join("", signatureChars);
        return modifiedSignature;
    }
}