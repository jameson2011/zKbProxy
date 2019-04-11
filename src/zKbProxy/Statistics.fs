namespace ZkbRedisqProxy

    open System

    type StreamStatistics = 
        {
            KillBufferSize: int
            LastKillPull: DateTime        
        } with 
        static member empty = { KillBufferSize = 0; 
                                LastKillPull = DateTime.MinValue }

    type Statistics =
        {
            ImportedKills: int64
            MainStreamStatistics: StreamStatistics
            SessionStreamStatistics: Map<string, StreamStatistics>
        }
        with static member empty = { Statistics.ImportedKills = 0L; 
                                                MainStreamStatistics = StreamStatistics.empty;
                                                SessionStreamStatistics = Map.empty}
