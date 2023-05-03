namespace VideoPlatform.YouTube;

internal class ReverseOperation : ISignatureModifyOperation
{
    public string ModifySignature(string signature, int argument)
    {
        var modifiedSignatureChars = signature.ToCharArray().Reverse().ToArray();
        var modifiedSignature = string.Join("", modifiedSignatureChars);
        return modifiedSignature;
    }
}