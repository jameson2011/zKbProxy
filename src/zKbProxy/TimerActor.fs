namespace ZkbProxy

open System

type TimerActor(interval: TimeSpan, callback)=

    let pipe = MailboxProcessor<unit>.Start(fun _ ->
        let rec loop() = async {
            
            do! Async.Sleep(int interval.TotalMilliseconds)
            callback()

            return! loop()
            }
        loop())

