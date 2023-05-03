namespace VideoPlatform.YouTube;

internal interface ISignatureModifyOperation
{
    int Index { get; set; }
    
    string ModifySignature(string signature);
}