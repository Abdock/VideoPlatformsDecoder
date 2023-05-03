namespace VideoPlatform.YouTube;

internal interface ISignatureModifyOperation
{
    string ModifySignature(string signature, int argument);
}