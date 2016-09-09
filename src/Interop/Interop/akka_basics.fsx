﻿


(*

    Akka.Net Fundamentals

*)


#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.DI.Autofac/lib/net45/Akka.DI.Autofac.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

open Akka
open Akka.Actor
open Akka.Configuration
open Akka.FSharp





(* 

    Basic messaging functionality 

*)

type Greet(who) = 
    member x.Who = who

type GreetingActor() as g =
    inherit ReceiveActor()
    do g.Receive<Greet>
        (fun (greet:Greet) -> printfn "\r\nReceived a greeting, hello: %s\r\n" greet.Who)

let greetingSystem = ActorSystem.Create("greeting-system")
let greeter = greetingSystem.ActorOf<GreetingActor> "greeter-actor"

"world" |> Greet |> greeter.Tell

greetingSystem.Terminate()



(* 
    
    Actor coordination functionality 

    An Actor is a like a thread instance with a mailbox. 

*)


type EchoServer =
    inherit Actor

    override x.OnReceive message =
        match message with
        | :? string as msg -> printfn "Hello %s\r\n" msg
        | _ ->  failwith "unknown message"

let echoSystem = ActorSystem.Create("echo-server")
let echo = echoSystem.ActorOf(Props(typedefof<EchoServer>, Array.empty))

echo <! "F#!"

echoSystem.Terminate()





(* 
    
    Simplified actor definition 

*)

let simpleSystem = ActorSystem.Create("simple-actor")

let echoServer = 
    spawn simpleSystem "echo-server"
    <| fun mailbox ->
            actor {
                let! message = mailbox.Receive()
                match box message with
                | :? string as msg -> printfn "Hello %s\r\n" msg
                | _ ->  failwith "unknown message"
            } 

echoServer <! "F#!"
simpleSystem.Terminate()



(*

    Event Stream

    The event stream is the main event bus of each actor system: 

        it is used for carrying log messages and Dead Letters and may be used by the user code 
        for other purposes as well. It uses Subchannel Classification which enables registering 
        to related sets of channels

*)

let eventSystem = ActorSystem.Create("event-stream")

let streamServer = 
    spawn eventSystem "EchoServer"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                match box message with
                | :? string -> 
                    printfn "Echo '%s'" message
                    return! loop()
                | _ ->  failwith "unknown message"
            } 
        loop()


let eventStream = eventSystem.EventStream

eventStream.Subscribe(echoServer, typedefof<string>)

eventStream.Publish("Anybody home?")
eventStream.Publish("Knock knock")

eventSystem.Terminate()