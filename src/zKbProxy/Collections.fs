namespace ZkbRedisqProxy

type CappedQueue<'a>(size: int)=
        
    let queue = new System.Collections.Generic.Queue<'a>()

    let trim()=
        let mutable toDelete = max 0 (queue.Count - size)
        while toDelete > 0 do
            queue.Dequeue() |> ignore
            toDelete <- toDelete - 1

    member __.Count with get() = queue.Count

    member __.Push(value: 'a)=
        queue.Enqueue value
        trim()

    member __.Pull()=
        match queue.Count with
        | 0 -> None
        | _ -> Some (queue.Dequeue())

type MutableCappedBuffer<'T> private (size, buffer: ResizeArray<'T>)=
    do if size <= 0 then invalidArg "size" "The size must be positive."
        
    new(size) = new MutableCappedBuffer<'T>(size, new ResizeArray<'T>(size))
    
    member this.Add(value: 'T)= 
        buffer.Insert(0, value)
        if(buffer.Count > size) then
            buffer.RemoveAt(buffer.Count - 1)
        this
               
    member __.Contains(value: 'T)= 
        buffer.Contains(value)

    member __.Items() = 
        seq {
            yield! buffer
        }
