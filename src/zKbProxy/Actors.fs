namespace ZkbProxy

    open System

        
    type IKillProviderActor = 
        abstract member Name: string with get
        abstract member GetLastPull: unit -> Async<DateTime>
        abstract member RequestKill: string -> Async<string option>
        abstract member RequestCachedKill: bool -> Async<string option>
        abstract member RequestCachedKillReset : unit -> Async<bool>
        abstract member RequestNextLiveKill: unit -> Async<string option>
        abstract member GetKillCount: unit -> Async<int64>

    type ActorMessage=
        | Start
        | Stop
        | GetNextKillFromSource of string * TimeSpan
        | KillJson of string
        | Trace of string * string
        | Info of string * string
        | Exception of string * Exception
        | Error of string * string
        | Warning of string * string
        | GetNextCachedKill of bool * AsyncReplyChannel<string option>
        | ResetCachedKills of AsyncReplyChannel<bool>
        | GetNextLiveKill of AsyncReplyChannel<string option>
        | GetKill of string * AsyncReplyChannel<string option>
        | GetStatistics of AsyncReplyChannel<Statistics>
        | SetKillCount of int64
        | KillProviderStats of string * int * DateTime
        | GetSession of string * AsyncReplyChannel<IKillProviderActor>
        | PurgeExpiredSessions
        | SessionPurged of string
        | GetLastPull of AsyncReplyChannel<DateTime>
        | Ping of AsyncReplyChannel<unit>
        | Get of string * AsyncReplyChannel<WebResponse>

    type MessageInbox = MailboxProcessor<ActorMessage>
    type PostMessage = ActorMessage -> unit
        
    type IActor =
        abstract member Post: ActorMessage -> unit
        abstract member Request: ActorMessage -> Async<'a>
        abstract member Ping: unit -> Async<unit>

    module Actors =        
        [<Literal>]
        let mainProviderName = ""

        let broadcast (posts: PostMessage list) (value: ActorMessage) =
            posts |> List.iter (fun p -> p value)

        let messageSource (source: string) (message: string) = 
            (source, message)
            