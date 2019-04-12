namespace ZkbProxy

open System
open MongoDB.Bson
open MongoDB.Driver
        
type private KillCache=
    {
        LiveKillQueue: CappedQueue<string>;
        LastObjectId: ObjectId option;
        Cycled: bool;
        LastPull: DateTime
    }
    with static member Empty size = { LiveKillQueue = new CappedQueue<string>(size);
                                        LastObjectId = None; 
                                        Cycled = false;
                                        LastPull = DateTime.MinValue }
      
            
type KillProviderActor(log: PostMessage, stats: PostMessage, config: Configuration, name: string)=
        
    let logException (ex: Exception) = ActorMessage.Error (typeof<KillProviderActor>.Name,ex.Message) |> log

    let dbCollection = MongoDb.defaultCollection config.MongoServer config.DbName config.DbCollection config.MongoUserName config.MongoPassword
                    
    let getKill id =          
        async {  
            let filterJson = sprintf "{'_v.package.killID': %s }" id                       
            let idFieldFilter = new JsonFilterDefinition<BsonDocument>(filterJson)
                                    
            try
                use! ps = dbCollection.FindAsync(idFieldFilter) |> Async.AwaitTask
                return ps.FirstOrDefault() |> Option.ofNull |> Option.bind Kills.toPackage
            with 
                | ex -> logException ex
                        return None
        }
            
    let getKillCount()=
        async {                
            try
                return! (dbCollection.CountAsync(fun _ -> true) |> Async.AwaitTask)
            with
            | e ->  logException e
                    return 0L
        }

    let getFirstCachedKill()=
        async {                
            try
                use! docs = dbCollection.FindAsync(fun d -> true) |> Async.AwaitTask
                
                let! doc = docs.FirstOrDefaultAsync() |> Async.AwaitTask
                return match doc with
                        | null -> None
                        | _ -> 
                            let objectId = Bson.getObjectId doc
                            match Kills.toPackage doc with
                            | Some d -> Some (objectId, d)
                            | _ -> None                                                          
            with ex ->  logException ex
                        return None
        }

    let getNextCachedKill(id: ObjectId) =
        async{
            try                                
                let filterJson = sprintf @"{_id: { $gt: ObjectId(""%s"") } }" (id.ToString())
                let idFieldFilter = new JsonFilterDefinition<BsonDocument>(filterJson)
                                    
                use! docs = dbCollection.FindAsync(idFieldFilter) |> Async.AwaitTask
                
                let! doc = docs.FirstOrDefaultAsync() |> Async.AwaitTask
                
                return match doc with
                        | null -> None
                        | d -> 
                            let objectId = Bson.getObjectId doc
                            match d |> Kills.toPackage with
                            | Some p -> Some (objectId, p)
                            | _ -> None
            with ex ->  logException ex
                        return None
        }
       

    let onGetKill (cache: KillCache) id (ch: AsyncReplyChannel<string option>) =
        async {
            let! doc = getKill id
                
            (match doc with
                | Some d -> d |> Bson.toJson |> Some 
                | _ ->      None)
            |> ch.Reply 

            return cache
        }

    let onGetNextLiveKill (cache: KillCache) (ch: AsyncReplyChannel<string option>) =
        async {
            let queue = cache.LiveKillQueue

            (match cache.LiveKillQueue.Pull() with
                | Some j -> 
                    ActorMessage.KillProviderStats (name, queue.Count, cache.LastPull) |> stats
                    Some j 
                | _ -> None)
            |> ch.Reply
            
            return { cache with LiveKillQueue = queue; LastPull = DateTime.UtcNow }
        }

    let onNewKillJson cache json =
        async {
            let queue = cache.LiveKillQueue
            queue.Push json
            ActorMessage.KillProviderStats (name, queue.Count, cache.LastPull) |> stats
            return { cache with LiveKillQueue = queue }
        }

    let onGetNextCachedKill (cache: KillCache) id (ch: AsyncReplyChannel<string option>) = 
        async {                
            let! resp = async {
                                return! (match id with                    
                                            | Some i -> getNextCachedKill i
                                            | None -> getFirstCachedKill() )
                            }

            let objId = match resp with
                            | Some (id,bson) ->                                     
                                    bson |> Bson.toJson |> Some |> ch.Reply
                                    Some id
                            | _ -> 
                                    ch.Reply None
                                    None

            let cycled = match id, objId, cache.Cycled with
                            | Some _, None, _ -> true
                            | _,_,cycled -> cycled

            return { cache with LastObjectId = objId; 
                                Cycled = cycled;
                                LastPull = DateTime.UtcNow }
        }


    let pipe = MessageInbox.Start(fun inbox ->
        let rec loop(cache: KillCache) = async{           
            let! msg = inbox.Receive()
            let! newCache = 
                try
                    match msg with
                    | GetKill (id,ch) ->            onGetKill cache id ch
                    | GetNextLiveKill ch ->         onGetNextLiveKill cache ch
                    | KillJson j ->                 onNewKillJson cache j
                    | GetNextCachedKill (cycle,ch) ->       
                                                    match cycle, cache.Cycled with
                                                    | false, true -> async {
                                                                            do! Async.Sleep(10000)
                                                                            ch.Reply None
                                                                            return cache }
                                                    | _ -> onGetNextCachedKill cache cache.LastObjectId ch
                    | ResetCachedKills ch ->        ch.Reply true
                                                    async { return { cache with LastObjectId = None; Cycled = false; } }
                    | GetLastPull ch ->             cache.LastPull |> ch.Reply
                                                    async { return cache }
                    | _ ->                          async { return cache }
                with e ->                           async { logException e; return cache }                                

            return! loop(newCache)
        }

        loop(KillCache.Empty config.LiveBufferSize)
    )

    do pipe.Error.Add(logException)
    do ActorMessage.KillProviderStats (name, 0, DateTime.MinValue) |> stats

    interface IKillProviderActor with
        member __.Name with get() = name

        member __.GetLastPull() = 
            pipe.PostAndAsyncReply ActorMessage.GetLastPull

        member __.RequestKill id =
            pipe.PostAndAsyncReply (fun ch -> ActorMessage.GetKill (id,ch) )
        
        member __.RequestCachedKill(cycle: bool) =
            pipe.PostAndAsyncReply (fun ch -> ActorMessage.GetNextCachedKill (cycle, ch) )
        
        member __.RequestCachedKillReset() =
            pipe.PostAndAsyncReply ActorMessage.ResetCachedKills

        member __.RequestNextLiveKill() = 
            pipe.PostAndAsyncReply ActorMessage.GetNextLiveKill        

        member __.GetKillCount()=
            getKillCount()

    interface IActor with            
        member __.Post(msg) = pipe.Post msg
        member __.Request(msg) = pipe.PostAndAsyncReply (fun ch -> msg)

        
        