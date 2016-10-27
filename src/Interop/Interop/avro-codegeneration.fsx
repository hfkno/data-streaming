

(*

    Generation of code and templates from AVRO schemata

*)


let rootFolder = "C:\\proj\\test\\ad_user-value\\"
let inputFile = rootFolder + "gentest.v0001.avsc"




#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../packages/Apache.Avro.1.7.7.2/lib/Avro.dll"

open Avro
open System.IO


let outDir = rootFolder

let schema = Schema.Parse(File.ReadAllText(inputFile));
let fschema = schema :?> RecordSchema
fschema.Namespace

let codeGen = new CodeGen();
codeGen.AddSchema(schema);
codeGen.GenerateCode();
codeGen.WriteTypes(outDir);




//#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
//#r "../packages/Microsoft.Hadoop.Avro.1.5.6/lib/net45/Microsoft.Hadoop.Avro.dll"
//#r "../packages/Microsoft.Hadoop.Avro.1.5.6/lib/net45/Microsoft.Hadoop.Avro.dll"
#r "../../dependencies/Microsoft.Avro/Microsoft.Hadoop.Avro.dll"

open Microsoft.Hadoop
open Microsoft.Hadoop.Avro.Schema
open Microsoft.Hadoop.Avro.Utils
open Microsoft.Hadoop


//let schemas = new 




// MS has generation code, but it is currently in limbo - the NuGet packages are outdated...
// The tool is available from the old codeplex solution, and can be compiled there...
// https://github.com/Azure/azure-sdk-for-net/issues/2322 -- this is the repo that should be active, but it seems to be missing the AVRO code.
// The code in question is an *internal* class... Microsoft.Hadoop.Avro.Utils.CodeGenerator

// Reflection to access the CodeGenerator
//var ass = Assembly.GetAssembly(typeof(Class2));
//var type = ass.GetType("ClassLibrary1.Class1");
//var prop = type.GetProperty("Test", BindingFlags.Static 
//    | BindingFlags.NonPublic);
//var s = (string)prop.GetValue(type, null);


// Command to generate code from the provided Tool
// C:\proj\onetime\hadoopsdk\bin\Unsigned\Release\Microsoft.Hadoop.Avro.Tools
// .\Microsoft.Hadoop.Avro.Tools codegen /i:C:\proj\test\ad_user-value\gentest.v0001.avsc /o:C:\proj\test\ad_user-value\





// ## Generation tool calls

//      IKVM.NET
//      Compiling the library through IKVM - this didn`t work because
//      -------------------------------------------------------------
//
//      PS C:\Users\aarcomy\Downloads\ikvmbin-7.2.4630.5\ikvm-7.2.4630.5\bin> .\ikvmc -target:library C:\Users\aarcomy\Downloads
//         \json-schema-avro-0.1.4\json-schema-avro-0.1.4\build\libs\json-schema-avro-0.1.4.jar
//
//
//
//      jni4net
//      Compiling a .Net bridge called through JNI
//      ------------------------------------------
//
//      # Had to force 32 bit mode on the generation tool - through a VS command window: 
//      c:\>corflags C:\app\dev\tools\jni4net\bin\proxygen.exe /32BIT+ /Force
//
//      # Had to change the proxy app.config, adding to a new "<runtime>" element:
//      # <loadFromRemoteSources enabled="true" /> a la http://docs.telerik.com/teststudio/troubleshooting-guide/test-execution-problems-tg/network-dll-access-net4-0
//
//      # Proxy generation:
//      c:\>C:\app\dev\tools\jni4net\bin\proxygen.exe C:\Users\aarcomy\Downloads\json-schema-avro-0.1.4\json-schema-avro-0.1.4\build\libs\json-schema-avro-0.1.4.jar -wd C:\proj\data-streaming\src\dependencies\json-schema\
//
//
//
//





