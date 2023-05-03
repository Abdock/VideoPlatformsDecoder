namespace VideoPlatform.YouTube;

internal class ReverseOperation : ISignatureModifyOperation
{
    public int Index { get; set; }

    public string ModifySignature(string signature)
    {
        var modifiedSignatureChars = signature.ToCharArray().Reverse().ToArray();
        var modifiedSignature = string.Join("", modifiedSignatureChars);
        return modifiedSignature;
    }
}