namespace VideoPlatform.YouTube;

internal class SliceOperation : ISignatureModifyOperation
{
    public string ModifySignature(string signature, int argument)
    {
        var modifiedSignatureChars = signature.ToCharArray().Skip(argument).ToArray();
        var modifiedSignature = string.Join("", modifiedSignatureChars);
        return modifiedSignature;
    }
}