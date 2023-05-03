namespace VideoPlatform.YouTube;

internal class SliceOperation : ISignatureModifyOperation
{
    public int Index { get; set; }

    public string ModifySignature(string signature)
    {
        var modifiedSignatureChars = signature.ToCharArray().Skip(Index).ToArray();
        var modifiedSignature = string.Join("", modifiedSignatureChars);
        return modifiedSignature;
    }
}