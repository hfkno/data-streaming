

(*

    Generation of code and templates from AVRO schemata

*)




//#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
//#r "../packages/Microsoft.Hadoop.Avro.1.5.6/lib/net45/Microsoft.Hadoop.Avro.dll"
//#r "../packages/Microsoft.Hadoop.Avro.1.5.6/lib/net45/Microsoft.Hadoop.Avro.dll"
#r "../../dependencies/Microsoft.Avro/Microsoft.Hadoop.Avro.dll"

open Microsoft.Hadoop
open Microsoft.Hadoop.Avro.Schema
open Microsoft.Hadoop.Avro.Utils
open Microsoft.Hadoop

let rootFolder = "C:\\proj\\test\\ad_user-value\\"
let inputFile = rootFolder + "gentest.v0001.avsc"


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
// .\Microsoft.Hadoop.Avro.Tools codegen /i:C:\proj\test\ad_user-value\gentest.v0001.avsc /o:C:\proj\test\ad_user-value\







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
