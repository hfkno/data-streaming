

(*

    Basic Akka.Net file reader -> webservice agency

*)


#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.DI.Autofac/lib/net45/Akka.DI.Autofac.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../../packages/FSPowerPack.Core.Community/Lib/net40/FSharp.PowerPack.dll"

open System
open System.IO
open Akka
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let handleContent content =
    printf "handling!\r\n"
    printf "whaaaaaa %s" content


let readFile (mailbox:Actor<System.Uri>) (file:System.Uri) =
    printf "readingFile!\r\n"
    // stream the file contents to the actor framework...
    let content = File.ReadAllLines(file.LocalPath)

    let handler =  spawn mailbox "console-writer" <| actorOf handleContent

    for line in content do
        handler <! line

    //spawn mailbox ("observer-" + Uri.EscapeDataString(filePath)) (observer filePath writer) |> ignore)


//let leverandørFilLeser (mailbox:Actor<System.Uri>) =
//    let rec readFileLoop() = actor {
//        let! msg = mailbox.Receive()
//
//        return! readFileLoop()
//    }
//    readFileLoop()

let observer filePath consoleWriter (mailbox:Actor<_>) =    
    let fsw = new FileSystemWatcher(
                        Path = filePath, 
                        Filter = "*.*",
                        EnableRaisingEvents = true, 
                        NotifyFilter = (NotifyFilters.FileName ||| NotifyFilters.LastWrite ||| NotifyFilters.LastAccess ||| NotifyFilters.CreationTime ||| NotifyFilters.DirectoryName)
                        )
    
    let fileHandler = spawn mailbox "reader" <| actorOf2 readFile
    
    // subscribe to incoming file system events - send them to consoleWriter
    let subscription = 
        [fsw.Created |> Observable.map(fun x -> x.ChangeType.ToString(), x.FullPath);]
        |> List.reduce Observable.merge
        //fsw.Created |> Observable.map(fun x -> x.ChangeType.ToString(), x.Name)
        |> Observable.filter(fun (changeType, fileName) -> fileName.EndsWith(".lsi"))
        |> Observable.subscribe(fun (changeType, fileName) -> 
                
                // Send file to parser
                fileHandler <! new System.Uri(fileName)
                consoleWriter <! fileName
            )

    // Freeing resources when terminating
    mailbox.Defer <| fun () -> 
        subscription.Dispose()
        fsw.Dispose()

    let rec loop () = actor {
        let! msg = mailbox.Receive()
        return! loop()
    }
    loop ()



// Create parser that loads and then delegates uploading line-by-line

let system = ActorSystem.Create("observer-system")

// create actor responsible for printing messages
let writer = spawn system "console-writer" <| actorOf (printfn "%A")

// create manager responsible for serving listeners for provided paths
let manager = spawn system "manager" <| actorOf2 (fun mailbox filePath ->
    spawn mailbox ("observer-" + Uri.EscapeDataString(filePath)) (observer filePath writer) |> ignore)

manager <! __SOURCE_DIRECTORY__ + "\\test\\"

system.Terminate()





let functionSystem = ActorSystem.Create("function-system")

let actorOfSink (f: 'a -> unit) = actorOf2 (fun _ msg -> f msg)
let print msg = printfn "Message recieved: %A" msg

let printActorRef = 
  actorOfSink print 
  |> spawn functionSystem "print-actor"

printActorRef <! 123
printActorRef <! "hello"