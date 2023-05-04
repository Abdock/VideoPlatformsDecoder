using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VideoPlatform.Logger;

namespace VideoPlatform.YouTube;

internal class YouTubeVideoInfo
{
    private readonly Regex _decodeUriRegex = new(@"=([$\w]+)\(decodeURIComponent\(", RegexOptions.Compiled);
    private readonly Regex _sliceFunctionRegex = new(@"\.splice\(\d+,\w+\)", RegexOptions.Compiled);
    private readonly Regex _swapFunctionRegex = new(@"(var\s\w=.*)\[\w+%\w+\.length", RegexOptions.Compiled);
    private readonly Regex _functionArgumentRegex = new(@"\d+", RegexOptions.Compiled);
    private readonly Regex _calledFunctionFromObjectRegex =
        new(@"([$\w]+\.)([$\w]+\(\w+,\d+\))", RegexOptions.Compiled);
    private readonly Regex _calledFunctionRegex = new(@"([$\w]+\(\w+,\d+\))", RegexOptions.Compiled);
    private readonly Regex _calledFunctionNameRegex = new(@"^\w+");
    private readonly Regex _clearFunctionBeginRegex = new(@"^\{", RegexOptions.Compiled);
    private readonly Regex _clearFunctionEndRegex = new(@"\}{2}$", RegexOptions.Compiled);
    private readonly Regex _signatureParameterRegex = new(@"^\w+=", RegexOptions.Compiled);
    private readonly Regex _urlParameterRegex = new("&url=", RegexOptions.Compiled);

    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }

    [JsonPropertyName("signatureCipher")]
    public string? SignatureCipher { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; init; }
    
    [JsonPropertyName("height")]
    public int Height { get; init; }
 
    [JsonIgnore]
    public bool IsVideo => MimeType.ToLower().Contains("video");

    [JsonIgnore]
    public bool IsUrlEncoded => Url == null && SignatureCipher != null;
    
    [JsonIgnore]
    public int Resolution => Width * Height;

    public void DecodeSignature(string baseJs, ILogger logger)
    {
        switch (SignatureCipher)
        {
            case null when Url != null:
                return;
            case null when Url == null:
                throw new InvalidOperationException("Signature cipher and url both null");
        }

        var cipher = WebUtility.UrlDecode(WebUtility.UrlDecode(SignatureCipher!));
        logger.LogMessage($"Signature cipher: {cipher}");
        const string queryAndSeparator = "&";
        var signatureParameter = cipher.Split(queryAndSeparator).First();
        logger.LogMessage($"{nameof(signatureParameter)} value: {signatureParameter}");
        var signature = _signatureParameterRegex.Replace(signatureParameter, "");
        logger.LogMessage($"{nameof(signature)} value: {signature}");
        const int argumentIndex = 1;
        var decodeUriFunction = _decodeUriRegex.Match(baseJs).Groups[argumentIndex].Value;
        logger.LogMessage($"{nameof(decodeUriFunction)} value: {decodeUriFunction}");
        var calledFunctionRegexes = new[]
        {
            @"\W" + Regex.Escape(decodeUriFunction) + @"=function(\(\w+\)\{[^\{]+\})",
            @"function " + Regex.Escape(decodeUriFunction) + @"(\(\w+\)\{[^\{]+\})"
        };
        var calledFunctions = calledFunctionRegexes
            .Select(pattern => GetCalledFunctions(baseJs, pattern))
            .ToList();
        var calledFunctionsWithArguments = calledFunctions
            .SelectMany(function => _calledFunctionRegex.Matches(function))
            .ToList();
        var calledFunctionsNames = calledFunctionsWithArguments
            .Select(function => _calledFunctionNameRegex.Match(function.Groups[argumentIndex].Value).Value)
            .ToHashSet();
        logger.LogMessage($"{nameof(calledFunctions)} value: {string.Join("; ", calledFunctions)}");
        logger.LogMessage($"{nameof(calledFunctionsWithArguments)} value: {string.Join("; ", calledFunctionsWithArguments)}");
        logger.LogMessage($"{nameof(calledFunctionsNames)} value: {string.Join("; ", calledFunctionsNames)}");
        var functionsOperation = new Dictionary<string, ISignatureModifyOperation>();
        foreach (var functionName in calledFunctionsNames)
        {
            var functionDeclaration = GetFunctionDeclaration(baseJs, functionName);
            logger.LogMessage($"{nameof(functionName)} value: {functionName}");
            logger.LogMessage($"{nameof(functionDeclaration)} value: {functionDeclaration}");
            if (_sliceFunctionRegex.IsMatch(functionDeclaration))
            {
                logger.LogMessage($"{functionName} recognized like slice function");
                functionsOperation[functionName] = new SliceOperation();
            }
            else if (_swapFunctionRegex.IsMatch(functionDeclaration))
            {
                logger.LogMessage($"{functionName} recognized like swap function");
                functionsOperation[functionName] = new SwapOperation();
            }
            else if (!functionDeclaration.Any())
            {
                logger.LogMessage($"{functionName} recognized like reverse function");
                functionsOperation[functionName] = new ReverseOperation();
            }
        }

        foreach (var match in calledFunctionsWithArguments)
        {
            logger.LogMessage($"{nameof(match)} value: {match.Value}");
            var functionName = _calledFunctionNameRegex.Match(match.Groups[1].Value).Value;
            var functionArgument = int.Parse(_functionArgumentRegex.Matches(match.Groups[1].Value).Last().Value);
            logger.LogMessage($"Function {functionName} arguments for function {functionArgument}");
            if (functionsOperation.TryGetValue(functionName, out var value))
            {
                logger.LogMessage($"Signature: {signature} before operation");
                signature = value.ModifySignature(signature, functionArgument);
                logger.LogMessage(
                    $"Signature: {signature} after operation. Operation name: {value.GetType().Name} argument for operation {functionArgument}");
            }
        }

        var url = _urlParameterRegex.Split(cipher).Last();
        logger.LogMessage($"Url splitted by cipher: {url}");
        Url = $"{url}&sig={signature}";
        logger.LogMessage($"Decoded url: {Url}");
    }

    private string GetFunctionDeclaration(string js, string functionName)
    {
        var functionNameRegex = Regex.Escape(functionName);
        const string functionBegin = @"[^$\w]";
        const string functionSignature = @":function\((\w+,\w+)\)(\{[^\{\}]+\})";
        var functionDeclarationPattern = functionBegin + functionNameRegex + functionSignature;
        var functionDeclarationRegex = new Regex(functionDeclarationPattern);
        var functionDeclaration = functionDeclarationRegex.Match(js).Value;
        
        functionDeclaration = _clearFunctionBeginRegex.Replace(functionDeclaration, "");
        functionDeclaration = _clearFunctionEndRegex.Replace(functionDeclaration, "");
        return functionDeclaration;
    }

    private string GetCalledFunctions(string js, string pattern)
    {
        var calledFunctionsWithObjects = Regex.Match(js, pattern).Value;
        var calledFunction = _calledFunctionFromObjectRegex.Replace(calledFunctionsWithObjects, match =>
        {
            var functionWithObject = match.Value;
            var function = _calledFunctionRegex.Match(functionWithObject).Value;
            return function;
        });
        return calledFunction;
    }
}