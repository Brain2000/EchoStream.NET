# EchoStream.NET
An in memory bi-directional stream that allows both writing and reading the same instance.  This is useful for situations where you need to pass a stream into a function that writes to it, but you then may want to in turn pass that stream into another function that reads from a stream. Normally steams cannot do this and are unidirectional.  This is a very specialized tool that is only required in certain circumstances, such as the PowerBak utility, which needs to split a stream to simultaneously write to both the file system and SQL.

# EchoStream Notes:
1) The constructor can set a max queue depth and if this level is reached then stream writes will block until a Read is performed which drops the queue depth back below the max level (no limit=0, default=10).

2) There is a public property called AlwaysCopyBuffer, which makes a copy of the buffer in the Write function. Setting this to true will safely allow the byte buffer to be reused after calling Write. Otherwise the caller will need to preserve the buffer because the stream has the same memory reference.

3) There is a public property called ReadTimeout/WriteTimeout, which controls how long the Read/Write function will block before returning a 0 (default=Infinite).

4) This class has been extensively tested to be thread safe for simultaneous async reads and writes.
