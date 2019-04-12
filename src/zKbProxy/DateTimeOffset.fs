namespace ZkbProxy
    
    open System

    module DateTimeOffset=

        let toUtc (x: DateTimeOffset) = x.UtcDateTime