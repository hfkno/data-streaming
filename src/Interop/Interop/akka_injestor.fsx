

(*

    Akka.Net Fundamentals

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



let system = ActorSystem.Create("observer-system")

let observer filePath consoleWriter (mailbox: Actor<_>) =    
    let fsw = new FileSystemWatcher(
                        Path = filePath, 
                        Filter = "*.*",
                        EnableRaisingEvents = true, 
                        NotifyFilter = (NotifyFilters.FileName ||| NotifyFilters.LastWrite ||| NotifyFilters.LastAccess ||| NotifyFilters.CreationTime ||| NotifyFilters.DirectoryName)
                        )
    // subscribe to all incoming events - send them to consoleWriter
    let subscription = 
        [fsw.Changed |> Observable.map(fun x -> x.Name + " " + x.ChangeType.ToString());
         fsw.Created |> Observable.map(fun x -> x.Name + " " + x.ChangeType.ToString());
         fsw.Deleted |> Observable.map(fun x -> x.Name + " " + x.ChangeType.ToString());
         fsw.Renamed |> Observable.map(fun x -> x.Name + " " + x.ChangeType.ToString());]
             |> List.reduce Observable.merge
             |> Observable.subscribe(fun x -> consoleWriter <! x)

    // don't forget to free resources at the end
    mailbox.Defer <| fun () -> 
        subscription.Dispose()
        fsw.Dispose()

    let rec loop () = actor {
        let! msg = mailbox.Receive()
        return! loop()
    }
    loop ()

// create actor responsible for printing messages
let writer = spawn system "console-writer" <| actorOf (printfn "%A")

// create manager responsible for serving listeners for provided paths
let manager = spawn system "manager" <| actorOf2 (fun mailbox filePath ->
    spawn mailbox ("observer-" + Uri.EscapeDataString(filePath)) (observer filePath writer) |> ignore)

manager <! "/Users/aaron/Documents/proj/data-streaming/src/Interop/Interop/test"