


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

let system = ActorSystem.Create("greeting-system")
let greeter = system.ActorOf<GreetingActor> "greeter-actor"

"world" |> Greet |> greeter.Tell

system.Terminate()



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

let sys = ActorSystem.Create("FSharp")
let echoServer = sys.ActorOf(Props(typedefof<EchoServer>, Array.empty))

echoServer <! "F#!"

sys.Terminate()





Configuration.defaultConfig()
//use sys = System.create "my-greeting-system" ()