namespace ZkbProxy

open System

type NoDbKillProviderActor(log: PostMessage, stats: PostMessage, config: Configuration, name: string)=
    
    let logException (ex: Exception) = ActorMessage.Error (typeof<KillProviderActor>.Name,ex.Message) |> log
    
    let getKillCount()=
        async {                
            return 0L
        }


    let onGetKill (cache: KillCache) id (ch: AsyncReplyChannel<string option>) =
        async {
            
            None |> ch.Reply

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
            ch.Reply None
            
            return { cache with LastObjectId = None; 
                                Cycled = false;
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

        loop(KillCache.Empty config.BufferSize)
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

        
        
