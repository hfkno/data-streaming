


(*

    Akka.Net Fundamentals

*)


#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.DI.Autofac/lib/net45/Akka.DI.Autofac.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

open System
open Akka
open Akka.Actor
open Akka.Configuration
open Akka.FSharp




(*

    An actor is a container for State, Behavior, a Mailbox, Children and a Supervisor Strategy.

    An “Actor” is really just an analog for human participants in a system. 

    An Actor:

        knows what kind of messages it can accept
        does some processing of each message
        can hold some state which is changed during message processing
        potentially changes its behavior based on the current state
        creates and stores references to child actors
        obtains references to other actors
        sends messages to children and other actors

*)



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

    Imperative and functional definition comparison of message handling

*)


// Imperative implementation of an Actor using F#
type MyActor() =
    inherit UntypedActor()

    override x.OnReceive message =
        match message with
        | :? string as msg -> Console.WriteLine msg
        | _ -> x.Unhandled message


// Functional implementation of an Actor
let myActor (mailbox:Actor<_>) (message:obj) =
    match message with
    | :? string as msg -> Console.WriteLine msg
    | _ -> mailbox.Unhandled message



(*

    Event Stream

    The event stream is the main event bus of each actor system: 

        it is used for carrying log messages and Dead Letters and may be used by the user code 
        for other purposes as well. It uses Subchannel Classification which enables registering 
        to related sets of channels

*)

let eventSystem = ActorSystem.Create("event-stream")

let streamServer = 
    spawn eventSystem "streamed-echo"
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

eventStream.Subscribe(streamServer, typedefof<string>)

eventStream.Publish("Anybody home?")
eventStream.Publish("Knock knock")

eventSystem.Terminate()





(*

    Typed Messages & Behaviour swapping

    Returning seperate handler functions based on user configurable logic

*)

type TypedMessages =
    | Hello of string
    | Hi

let typedSystem = ActorSystem.Create("typed-messages")

let typedServer = 
    spawn typedSystem "EchoServer"
    <| fun mailbox ->
        let rec replyInRussian() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | Hello name -> printfn "Привіт %s" name
                | Hi -> printfn "Привіт!"

                return! replyInEnglish()
            } 
        and replyInEnglish() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | Hello name -> printfn "Hello there %s..." name
                | Hi -> printfn "Hallo!"

                return! replyInRussian()
            } 

        replyInRussian()

typedServer <! Hello "Major Obstruction"
typedServer <! Hello "Mr"
typedServer <! Hi

typedSystem.Terminate()




(*

    Defining a simplified actor based on functions

    ...

*)

let functionSystem = ActorSystem.Create("function-system")

let actorOfSink (f: 'a -> unit) = actorOf2 (fun _ msg -> f msg)
let print msg = printfn "Message recieved: %A" msg

let printActorRef = 
  actorOfSink print 
  |> spawn functionSystem "print-actor"

printActorRef <! 123
printActorRef <! "hello"


(*
    ...

    Defining a stateless converter
    Accepts a message and sends it to another actor in a specified format

*)

let square msg = msg * msg

let actorOfConvert f outputRef = 
    actorOf2 (fun _ msg -> outputRef <! f msg)

let squareActorRef =
    actorOfConvert square printActorRef
    |> spawn functionSystem "square-actor"

squareActorRef <! 9



(* 

    ...

    State management in functions

    Using a recursive actor definition which will return a new actor based on the initial state
    This preserves functional immutability while maintaining state

*)

let printIndex index msg =
  printfn "Message [%i] received: %A" index msg
  index + 1

let actorOfStatefulSink f initialState (mailbox : Actor<'a>) =

  let rec imp lastState =
    actor {
      let! msg = mailbox.Receive()
      let newState = f lastState msg
      return! imp newState
    }

  imp initialState

let printIndexActorRef = 
  actorOfStatefulSink printIndex 1
  |> spawn functionSystem "print-ix-actor"

printIndexActorRef <! 3
printIndexActorRef <! 5



(*

    ...

    Stateful Conversion

    A new actor is sent the processing result while the state is preserved

*)

let squareAndSum sum msg =
  let result = sum + (msg * msg)
  (result, result)

let actorOfStatefulConvert f initialState outputRef (mailbox : Actor<'a>) =

  let rec imp lastState =
    actor {
      let! msg = mailbox.Receive()
      let (result, newState) = f msg lastState
      outputRef <! result
      return! imp newState
    }

  imp initialState

let squareAndSumActorRef = 
  actorOfStatefulConvert squareAndSum 0 printIndexActorRef
  |> spawn functionSystem "square-sum-actor"

squareAndSumActorRef <! 3
squareAndSumActorRef <! 4








(*

    Basic child definition and message passing

*)


let firstChildActor (mailbox:Actor<_>) =
  let rec loop() = actor {
      let! message = mailbox.Receive()
      printfn "Child says: %A" message
      return! loop()
  }
  loop()


let firstActor (mailbox:Actor<_>) =
  let myFirstChildActor = spawn mailbox.Context "myFirstChildActor" firstChildActor

  let rec loop() = actor {
      let! message = mailbox.Receive()
      printfn "Parent says: %A" message
      myFirstChildActor <! message
      return! loop()
  }
  loop()

let myActorSystem = ActorSystem.Create("parent-child")
let myFirstActor = spawn myActorSystem "myFirstActor" firstActor
myFirstActor <! "Hello"
myActorSystem.Terminate()
