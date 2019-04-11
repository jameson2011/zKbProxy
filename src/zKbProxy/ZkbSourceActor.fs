namespace ZkbRedisqProxy

open System
    
type ZkbSourceActor(log: PostMessage, forward: PostMessage)=
    let msgSource = Actors.messageSource typeof<ZkbSourceActor>.Name
    let logInfo = msgSource >> ActorMessage.Info >> log
    let logException (ex: Exception) =  ex.Message |> msgSource |> ActorMessage.Error |> log
    let logTrace = msgSource >> ActorMessage.Trace >> log
    let standoffWait = TimeSpan.FromSeconds(60.)
    let httpClient = Web.httpClient()
    let getData = Web.getData httpClient    
    let getKillId = Bson.ofJson >> Kills.getKillId
        
    let addOrIgnore =
        let dupeBuffer = new MutableCappedBuffer<string>(10)
        (fun id ->  if dupeBuffer.Contains id then
                        false
                    else
                        dupeBuffer.Add id |> ignore
                        true)
            
    let onNext (inbox: MessageInbox) url = 
        async {                                
            let! resp = getData url

            let waitTime = match resp.Status with
                                | HttpStatus.OK ->                 
                                    match resp.Message with
                                    | @"{""package"":null}" -> "No data received from zKb." |> logTrace
                                    | json ->   
                                                let tracemsg = match getKillId json with
                                                                | Some id -> 
                                                                        match addOrIgnore id with
                                                                        | true ->   ActorMessage.KillJson json |> forward
                                                                                    (sprintf "KillID %s received from source." id)
                                                                        | _ ->  sprintf "KillID %s ignored as a duplicate" id
                                                                | _ -> "Unrecognised message (no killID) receive from source."

                                                tracemsg |> logInfo
                                    TimeSpan.Zero
                                | HttpStatus.TooManyRequests -> 
                                    ActorMessage.Warning ("zKB", "zKB reported too many requests.") |> log
                                    standoffWait
                                | HttpStatus.Unauthorized -> 
                                    ActorMessage.Warning ("zKB", "zKB reported unauthorized.") |> log
                                    standoffWait
                                | HttpStatus.Error ->                                         
                                    ActorMessage.Error ("zKB", resp.Message) |> log
                                    standoffWait
            inbox.Post (GetNextKillFromSource (url, waitTime))

        }
        
    let pipe = MessageInbox.Start(fun inbox ->
        let rec loop() = async{
            
            let! msg = inbox.Receive()

            match msg with
            | Stop ->   
                "Stopped kill source." |>  logInfo
                    
            | GetNextKillFromSource (url,wait) ->
                let! w = Async.Sleep((int wait.TotalMilliseconds))                                                
                try
                    do! onNext inbox url 
                with ex -> 
                    logException ex
            | _ -> ignore 0

            return! loop()
        }

        loop()
    )

    do pipe.Error.Add(logException)

    interface IActor with            
        member __.Post(msg) = pipe.Post msg
        member __.Request(msg) = pipe.PostAndAsyncReply (fun ch -> msg)