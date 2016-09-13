


#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.DI.Autofac/lib/net45/Akka.DI.Autofac.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../../packages/FsPickler/lib/net45/FsPickler.dll"

open System
open Akka
open Akka.Actor
open Akka.Configuration
open Akka.FSharp




// Lesson 1

module Actors1 =
    type Command = 
    | Start
    | Continue
    | Message of string
    | Exit

    let (|Message|Exit|) (str:string) =
        match str.ToLower() with
        | "exit" -> Exit
        | _ -> Message(str)


    let consoleReaderActor (consoleWriter: IActorRef) (mailbox: Actor<_>) message = 
        let line = Console.ReadLine ()
        match line with
        | Exit -> mailbox.Context.System.Terminate() |> ignore
        | Message(input) -> 
            consoleWriter <! input
            mailbox.Self  <! Continue

    let consoleWriterActor message = 
        let (|Even|Odd|) n = if n % 2 = 0 then Even else Odd
    
        let printInColor color message =
            Console.ForegroundColor <- color
            Console.WriteLine (message.ToString ())
            Console.ResetColor ()

        match message.ToString().Length with
        | 0    -> printInColor ConsoleColor.DarkYellow "Please provide an input.\n"
        | Even -> printInColor ConsoleColor.Red "Your string had an even # of characters.\n"
        | Odd  -> printInColor ConsoleColor.Green "Your string had an odd # of characters.\n"


let printInstructions () =
    Console.WriteLine "Write whatever you want into the console!"
    Console.Write "Some lines will appear as"
    Console.ForegroundColor <- ConsoleColor.Red
    Console.Write " red"
    Console.ResetColor ()
    Console.Write " and others will appear as"
    Console.ForegroundColor <- ConsoleColor.Green
    Console.Write " green! "
    Console.ResetColor ()
    Console.WriteLine ()
    Console.WriteLine ()
    Console.WriteLine "Type 'exit' to quit this application at any time.\n"

//[<EntryPoint>]
let main argv = 
    let myActorSystem = ActorSystem.Create "MyActorSystem" //(Configuration.load ())
    printInstructions ()
    let consoleWriterActor = spawn myActorSystem "consoleWriterActor" (actorOf Actors1.consoleWriterActor)
    let consoleReaderActor = spawn myActorSystem "consoleReaderActor" (actorOf2 (Actors1.consoleReaderActor consoleWriterActor))
    consoleReaderActor <! Actors1.Start
    myActorSystem.WhenTerminated.Wait()
    0

let emtpy : string array = [||]
main emtpy 



// Lesson 2



module Actors2 =
        
    let (|Message|Exit|) (str:string) =
        match str.ToLower() with
        | "exit" -> Exit
        | _ -> Message(str)

    let consoleReaderActor (consoleWriter: IActorRef) (mailbox: Actor<_>) message = 
        let (|EmptyMessage|MessageLengthIsEven|MessageLengthIsOdd|) (msg:string) = 
            match msg.Length, msg.Length % 2 with
            | 0,_ -> EmptyMessage
            | _,0 -> MessageLengthIsEven
            | _,_ -> MessageLengthIsOdd

        let doPrintInstructions () =
            Console.WriteLine "Write whatever you want into the console!"
            Console.WriteLine "Some entries will pass validation, and some won't...\n\n"
            Console.WriteLine "Type 'exit' to quit this application at any time.\n"
            
        let getAndValidateInput () = 
            let line = Console.ReadLine()
            match line with
            | Exit -> mailbox.Context.System.Terminate ()
            | Message(input) -> 
                match input with
                | EmptyMessage -> 
                    mailbox.Self <! InputError ("No input received.", ErrorType.Null) |> ignore
                | MessageLengthIsEven -> 
                    consoleWriter <! InputSuccess ("Thank you! Message was valid.")
                    mailbox.Self  <! Continue
                | _ -> 
                    mailbox.Self <! InputError ("Invalid: input had odd number of characters.", ErrorType.Validation)

        match box message with
        | :? Command as command ->
            match command with
            | Start -> doPrintInstructions ()
            | _ -> ()
        | :? InputResult as inputResult ->
            match inputResult with
            | InputError(_,_) as error -> consoleWriter <! error
            | _ -> ()
        | _ -> ()
        getAndValidateInput ()

    let consoleWriterActor message = 
        let (|Even|Odd|) n = if n % 2 = 0 then Even else Odd
    
        let printInColor color message =
            Console.ForegroundColor <- color
            Console.WriteLine (message.ToString ())
            Console.ResetColor ()

        match box message with
        | :? InputResult as inputResult ->
            match inputResult with
            | InputError (reason,_) -> printInColor ConsoleColor.Red reason
            | InputSuccess reason -> printInColor ConsoleColor.Green reason
        | _ -> printInColor ConsoleColor.Yellow (message.ToString ())



let main2 argv = 
    let myActorSystem = System.create "MyActorSystem" (Configuration.load ())
    let consoleWriterActor = spawn myActorSystem "consoleWriterActor" (actorOf Actors2.consoleWriterActor)
    let consoleReaderActor = spawn myActorSystem "consoleReaderActor" (actorOf2 (Actors2.consoleReaderActor consoleWriterActor))
    consoleReaderActor <! Start
    myActorSystem.AwaitTermination ()
    0