// See https://aka.ms/new-console-template for more information

using RATools;

Jamiras.Services.CoreServices.RegisterServices();

var cli = new RAScriptCLI();
var result = cli.ProcessArgs(args);
if (result == ReturnCode.Proceed)
    result = cli.Run();

return (int)result;
