namespace ZkbProxy

open System
open MongoDB.Bson
    

type private SessionCache = 
    {
        SessionTimeout: TimeSpan;
        Sessions: Map<string, IKillProviderActor>;
    }
    
type Session = 
    { _id: string;
        name: string;
    }

type SessionProviderActor(log: PostMessage, stats: PostMessage, config: Configuration, mainProvider: IKillProviderActor, createKillProvider: string -> IKillProviderActor)=
    let msgSource = Actors.messageSource typeof<SessionProviderActor>.Name 
    let logException (ex: Exception) = ex.Message |> msgSource |> ActorMessage.Error |> log
    let logTrace = msgSource >> ActorMessage.Trace >> log 
    let logInfo = msgSource >> ActorMessage.Info >> log 

    let dbCol = if not config.NoCache then
                    MongoDb.sessionCollection config.MongoServer config.DbName config.SessionsDbCollection config.MongoUserName config.MongoPassword |> Some
                else None

    let write = match dbCol with
                | Some col ->   (fun (doc: BsonDocument) -> MongoDb.upsert col doc)
                | None ->       (fun _ -> 0 |> ignore )
    
    let delete = match dbCol with   
                    | Some col ->   (fun id -> MongoDb.delete col id)
                    | None ->       (fun _ -> 0 |> ignore)

    let existingSessions = match dbCol with     
                            | Some col ->   MongoDb.query<Session> col
                                                |> Seq.map (fun s -> s.name) 
                                                |> List.ofSeq
                            | None ->       [] 

                            |> List.map (fun n -> (n, createKillProvider n))
                            |> Map.ofList
    do if existingSessions.Count > 0 then
        existingSessions 
                |> Seq.map (fun x -> sprintf "Starting session %s." x.Key) 
                |> Seq.iter logInfo

    let sessionCache = {    SessionTimeout = config.SessionTimeout; 
                            Sessions = existingSessions; 
                        }

    let purgeSessions (cache: SessionCache) =
        
        let expiry = DateTime.UtcNow.Add(-cache.SessionTimeout)
        let expired (provider: IKillProviderActor) = 
            let dt = provider.GetLastPull() |> Async.RunSynchronously
            dt < expiry

        let victimKeys = cache.Sessions |> Seq.filter (fun kvp -> expired kvp.Value)
                                        |> Seq.map (fun kvp -> kvp.Key)
                                        |> Array.ofSeq

        if victimKeys |> Seq.exists (fun _ -> true) then
            "Starting session purge..." |> logInfo
            
            let newMap = victimKeys |> Seq.fold (fun (m: Map<_,_>) k -> m.Remove(k)) cache.Sessions
            victimKeys |> Seq.map ActorMessage.SessionPurged |> Seq.iter stats
            victimKeys |> Seq.iter delete 
            victimKeys |> Seq.map (sprintf "Purged session %s.") |> Seq.iter logInfo
            sprintf "Finished session purge. %d purged %d survivor(s)." victimKeys.Length newMap.Count |> logInfo
            
            { cache with Sessions = newMap; }
        else
            "Session purge complete. No victims found." |> logTrace
            cache

    
    let onGetSession name (cache: SessionCache) (channel: AsyncReplyChannel<IKillProviderActor>)= 
        let sessions,provider = 
            match name with
            | Actors.mainProviderName -> cache.Sessions,mainProvider
            | name ->   if cache.Sessions.ContainsKey(name) |> not then
                            let provider = createKillProvider name
                            let sessions = cache.Sessions.Add(name, provider)
                            { Session._id = name; name = name} |> Bson.ofObject |> write
                            sprintf "Created new session %s. %d active session(s)." name sessions.Count |> logInfo
                            sessions, provider
                        else 
                            cache.Sessions, cache.Sessions.[name]
        provider |> channel.Reply
        { cache with Sessions = sessions }
        

    let pipe = MessageInbox.Start(fun inbox ->
            let rec loop(cache: SessionCache) = async{           
                let! msg = inbox.Receive()
                
                let! newCache = 
                    try
                        match msg with
                        | GetSession (name,ch) ->       let newCache = onGetSession name cache ch                                                        
                                                        async { return newCache }
                        | KillJson _ ->                 let providers = cache.Sessions  |> Seq.map (fun kvp -> (kvp.Value :?> IActor).Post)
                                                                                        |> List.ofSeq
                                                        Actors.broadcast providers msg
                                                        async { return cache }
                        | PurgeExpiredSessions _ ->     async { return purgeSessions cache }
                        | Ping ch ->                    ignore 0 |> ch.Reply
                                                        async { return cache }
                        | _ ->                          async { return cache }
                    with e ->                           async { logException e; return cache }                                

                return! loop(newCache)
            }

            loop(sessionCache)
        )

    do pipe.Error.Add(logException)

    let purgeFrequency = TimeSpan.FromSeconds(30.)
    let purgeTimer = new TimerActor(purgeFrequency, fun () -> pipe.Post(ActorMessage.PurgeExpiredSessions))
    

    member __.MainProvider() = mainProvider

    member __.GetSession(sessionId) =
        pipe.PostAndAsyncReply (fun ch -> ActorMessage.GetSession (sessionId, ch) )

    interface IActor with            
        member __.Post(msg) = pipe.Post msg
        member __.Request(msg) = pipe.PostAndAsyncReply (fun ch -> msg)
        member __.Ping() = pipe.PostAndAsyncReply (fun ch -> Ping ch)

