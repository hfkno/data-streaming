
#load "../../packages/FSharp.Formatting/FSharp.Formatting.fsx"
open FSharp.Literate
open System.IO


/// Return path relative to the current file location
let relative subdir = Path.Combine(__SOURCE_DIRECTORY__, subdir)

// Create output directories & copy content files there
if not (Directory.Exists(relative "output")) then
  Directory.CreateDirectory(relative "output") |> ignore
  Directory.CreateDirectory (relative "output/content") |> ignore
  
// for fileInfo in DirectoryInfo(relative "../../docs/files/content").EnumerateFiles() do
//   fileInfo.CopyTo(Path.Combine(relative "output/content", fileInfo.Name)) |> ignore


/// Processes a single F# Script file and produce HTML output
let processScriptAsHtml () =
  let script = relative "ad_vismae_integration.fsx"
  let output = relative "output/ad_visma.html"
  let template = relative "template-file.html"
  Literate.ProcessScriptFile(script, template, output)
  printfn "\r\n\r\n\r\n-- Documentation generated --\r\n\r\n\r\n"


let makeDocs () = processScriptAsHtml ()


makeDocs()
//.