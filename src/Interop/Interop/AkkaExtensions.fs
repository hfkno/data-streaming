namespace Interop.Lifecycle


//Copyright (c) 2015 Compose It
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of 
//this software and associated documentation files (the "Software"), to deal in 
//the Software without restriction, including without limitation the rights to 
//use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
//the Software, and to permit persons to whom the Software is furnished to do so, 
//subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all 
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
//FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR 
//COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
//IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN 
//CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.



module Lifecycle =
    
    open Akka.Actor
    open Akka.FSharp
    open Akka.FSharp.Linq
    open Microsoft.FSharp.Linq


    type Foo = 
        {
            rawr : string   
        }

    type LifecycleOverride<'Message, 'Returned> =
        {
            PreStart    : (FunActor<'Message, 'Returned> -> (unit -> unit) -> unit) option;
            PostStop    : (FunActor<'Message, 'Returned> -> (unit -> unit) -> unit) option;
            PreRestart  : (FunActor<'Message, 'Returned> -> exn -> obj -> (exn * obj -> unit) -> unit) option; /// Start here!  Want to pass the message to the handler defined by the user.  Currently it's just being passed the bases handler, not the info...
                                                                                                 // The passing works out... it looks like FunActor does not have access to the context...
            PostRestart : (FunActor<'Message, 'Returned> -> exn -> (exn -> unit) -> unit) option;
        }

    let defOvrd = {PreStart = None; PostStop = None; PreRestart = None; PostRestart = None}
    
    type FunActorExt<'Message, 'Returned>(actor : Actor<'Message> -> Cont<'Message, 'Returned>, overrides : LifecycleOverride<'Message ,'Returned>) =
        inherit FunActor<'Message, 'Returned>(actor)

        member __.BasePreStart() = base.PreStart ()
        member __.BasePostStop() = base.PostStop ()
        member __.BasePreRestart(exn, msg) = base.PreRestart (exn, msg)
        member __.BasePostRestart(exn) = base.PostRestart (exn)

        override x.PreStart() = 
            match overrides.PreStart with
            | None -> x.BasePreStart ()
            | Some o -> o x x.BasePreStart
        override x.PostStop() =
            match overrides.PostStop with
            | None -> x.BasePostStop ()
            | Some o -> o x x.BasePostStop
        override x.PreRestart(exn, msg) =
            match overrides.PreRestart with
            | None -> x.BasePreRestart (exn, msg)
            | Some o -> o x exn msg x.BasePreRestart
        override x.PostRestart(exn) =
            match overrides.PostRestart with
            | None -> x.BasePostRestart (exn)
            | Some o -> o x exn x.BasePostRestart

    type ExpressionExt = 
        static member ToExpression(f : System.Linq.Expressions.Expression<System.Func<FunActorExt<'Message, 'v>>>) = toExpression<FunActorExt<'Message, 'v>> f
        static member ToExpression<'Actor>(f : Quotations.Expr<(unit -> 'Actor)>) = toExpression<'Actor> (QuotationEvaluator.ToLinqExpression f)  

    /// <summary>
    /// Spawns an actor using specified actor computation expression, with custom spawn option settings.
    /// The actor can only be used locally. 
    /// </summary>
    /// <param name="actorFactory">Either actor system or parent actor</param>
    /// <param name="name">Name of spawned child actor</param>
    /// <param name="f">Used by actor for handling response for incoming request</param>
    /// <param name="options">List of options used to configure actor creation</param>
    /// <param name="overrides">Functions used to override standard actor lifetime</param>
    let spawnOptOvrd (actorFactory : IActorRefFactory) (name : string) (f : Actor<'Message> -> Cont<'Message, 'Returned>) 
        (options : SpawnOption list) (overrides : LifecycleOverride<'Message, 'Returned>) : IActorRef = 
        let e = ExpressionExt.ToExpression(fun () -> new FunActorExt<'Message, 'Returned>(f, overrides))
        let props = applySpawnOptions (Props.Create e) options
        actorFactory.ActorOf(props, name)


    /// <summary>
    /// Spawns an actor using specified actor computation expression, with custom spawn option settings.
    /// The actor can only be used locally. 
    /// </summary>
    /// <param name="actorFactory">Either actor system or parent actor</param>
    /// <param name="name">Name of spawned child actor</param>
    /// <param name="f">Used by actor for handling response for incoming request</param>
    /// <param name="options">List of options used to configure actor creation</param>
    /// <param name="overrides">Functions used to override standard actor lifetime</param>
    let spawnOptOvrd2 (actorFactory : IActorRefFactory) (name : string) (f : Actor<'Message> -> Cont<'Message, 'Returned>) 
        (options : SpawnOption list) (overrides : LifecycleOverride<'Message, 'Returned>) : IActorRef = 
        let e = ExpressionExt.ToExpression(fun () -> new FunActorExt<'Message, 'Returned>(f, overrides))
        let props = applySpawnOptions (Props.Create e) options
        actorFactory.ActorOf (props, name)



    /// <summary>
    /// Spawns an actor using specified actor computation expression.
    /// The actor can only be used locally. 
    /// </summary>
    /// <param name="actorFactory">Either actor system or parent actor</param>
    /// <param name="name">Name of spawned child actor</param>
    /// <param name="f">Used by actor for handling response for incoming request</param>
    /// <param name="overrides">Functions used to override standard actor lifetime</param>
    let spawnOvrd (actorFactory : IActorRefFactory) (name : string) (f : Actor<'Message> -> Cont<'Message, 'Returned>)
        (overrides : LifecycleOverride<'Message, 'Returned>) : IActorRef = 
        spawnOptOvrd actorFactory name f [] overrides