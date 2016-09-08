


(*

    Basic Akka.Net functionality

*)


#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.DI.Autofac/lib/net45/Akka.DI.Autofac.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"


open Akka
open Akka.Actor
open Akka.FSharp

type Greet(who) = 
    member x.Who = who

type GreetingActor() as g =
    inherit ReceiveActor()
    do g.Receive<Greet>(fun (greet:Greet) -> printfn "\r\nGiving a greeting: %s\r\n" greet.Who)



Configuration.defaultConfig()
//use sys = System.create "my-greeting-system" ()
let system = ActorSystem.Create("GreetingSystem")
let greeter = system.ActorOf<GreetingActor> "greeter"

"World" |> Greet |> greeter.Tell




