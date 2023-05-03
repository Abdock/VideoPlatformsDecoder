using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

    public void DecodeSignature(string baseJs)
    {
        switch (SignatureCipher)
        {
            case null when Url != null:
                return;
            case null when Url == null:
                throw new InvalidOperationException("Signature cipher and url both null");
        }

        var cipher = WebUtility.UrlDecode(WebUtility.UrlDecode(SignatureCipher!));
        const string queryAndSeparator = "&";
        var signatureParameter = cipher.Split(queryAndSeparator).First();
        var signature = _signatureParameterRegex.Replace(signatureParameter, "");
        const int argumentIndex = 1;
        var decodeUriFunction = _decodeUriRegex.Match(baseJs).Groups[argumentIndex].Value;
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
        var functionsOperation = new Dictionary<string, ISignatureModifyOperation>();
        foreach (var functionName in calledFunctionsNames)
        {
            var functionDeclaration = GetFunctionDeclaration(baseJs, functionName);
            if (_sliceFunctionRegex.IsMatch(functionDeclaration))
            {
                functionsOperation[functionName] = new SliceOperation();
            }
            else if (_swapFunctionRegex.IsMatch(functionDeclaration))
            {
                functionsOperation[functionName] = new SwapOperation();
            }
            else if (!functionDeclaration.Any())
            {
                functionsOperation[functionName] = new ReverseOperation();
            }
        }

        foreach (var match in calledFunctionsWithArguments)
        {
            var functionName = _calledFunctionNameRegex.Match(match.Groups[1].Value).Value;
            var functionArgument = int.Parse(_functionArgumentRegex.Match(match.Groups[0].Value).Value);
            if (functionsOperation.TryGetValue(functionName, out var value))
            {
                value.Index = functionArgument;
                signature = value.ModifySignature(signature);
            }
        }

        var url = _urlParameterRegex.Split(cipher).Last();
        Url = $"{url}&sig={signature}";
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