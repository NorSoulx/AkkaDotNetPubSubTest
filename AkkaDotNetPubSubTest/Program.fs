namespace AS
module myActors =
    open Akka.Actor 
    open Akka.FSharp
    open Akka.FSharp.System
    open Akka.Configuration
    open System.Threading
    open System.IO
    let system = create "PubSub" <| Configuration.defaultConfig ()

    type Message =
        | Subscribe
        | Unsubscribe
        | Msg of IActorRef * int * string

    let loggerA2 (name:string) =
        let mutable counter=0
        let outputStats=1000
        spawn system name
            (actorOf2 (fun mailbox msg ->
                match msg with
                | Msg (sender, idx, content) ->
                    counter <- counter+1
                    if counter % outputStats = 0 then 
                      printfn "[%d,%s] %A[%d] %s" counter (System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff")) (sender.Path) idx content
                | _ -> () ))


    let subscriberA2 (name:string) (logger:IActorRef) =
        let maxMsgs = 100000 
        spawn system name
            (actorOf2 (fun mailbox msg ->
                let eventStream = mailbox.Context.System.EventStream
                match msg with
                | Msg (sender, idx, content) ->
                    if idx < maxMsgs then
                        logger <! Msg (mailbox.Self,idx,content)
                        sender <! Msg (mailbox.Self,idx+1, ("from "+name))  
                | Subscribe -> subscribe typeof<Message> mailbox.Self eventStream |> ignore
                | Unsubscribe -> unsubscribe typeof<Message> mailbox.Self eventStream |> ignore ))

    let publisherA2 (name:string) = 
        spawn system name
            (actorOf2 (fun mailbox msg ->
                publish msg mailbox.Context.System.EventStream))


    let testActorsA2 =
        printfn "Begin"
        let numSubscribers = 10
        let logger = loggerA2 "logger"
        let subscribers = [ for i in 1 .. numSubscribers -> subscriberA2 ("sub"+(string i)) logger]
        for sub in subscribers do sub <! Subscribe
        let pubA = publisherA2 "pubA"
        Async.Sleep 5000 |> Async.RunSynchronously
        pubA  <! Msg (pubA, 0, "start") 
        Async.Sleep 5000 |> Async.RunSynchronously
        for sub in subscribers do sub <! Unsubscribe
        Async.Sleep 60000 |> Async.RunSynchronously
        printfn "End"


    [<EntryPoint>]
    let main argv = 
        printfn "%A" argv
        testActorsA2 
        Thread.Sleep(5000)
        System.Console.ReadLine() |> ignore
        0 // return an integer exit code
