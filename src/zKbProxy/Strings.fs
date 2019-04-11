namespace ZkbRedisqProxy
    open System

    module Strings=

        let toString (value: 'a) = value.ToString()

        let toLower (value: string) = value.ToLower()

        let tryToInt (value: string) =
            Int32.TryParse value

        let split (delim: string) (value: string) = 
            value.Split(delim.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)

        let join (delim: string) (values: seq<string>) = 
            System.String.Join(delim, values)

        let toInts (value: string) = 
            value   |> split ","
                    |> Seq.map tryToInt 
                    |> Seq.filterMap
                    |> List.ofSeq

        let (|NullOrWhitespace|_|) value=
            if String.IsNullOrWhiteSpace value then Some value else None
