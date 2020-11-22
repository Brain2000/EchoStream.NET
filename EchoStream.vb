Imports System.IO
Imports System.Threading
Imports System.Collections.Concurrent
Imports System.Runtime

''' <summary>
''' EchoStream.NET<br />
''' Written By Brian Coverstone - December 2018<br />
''' <br />
''' An in memory bi-directional stream that allows both writing And reading the same instance.<br />
''' This is useful for situations where you need to pass a stream into a function that writes to it, but you then may want to in turn pass that stream into another function that reads from a stream.<br />
''' Normally steams cannot do this and are unidirectional.<br />
''' This is a very specialized tool that is only required in certain circumstances, such as the PowerBak utility, which needs to split a stream to simultaneously write to both the file system and SQL.
''' </summary>
Public Class EchoStream
    Inherits Stream

    Public Overrides ReadOnly Property CanTimeout As Boolean = True
    Public Overrides Property ReadTimeout As Integer = Timeout.Infinite
    Public Overrides Property WriteTimeout As Integer = Timeout.Infinite
    Public Overrides ReadOnly Property CanRead As Boolean = True
    Public Overrides ReadOnly Property CanSeek As Boolean = False
    Public Overrides ReadOnly Property CanWrite As Boolean = True

    Public Property CopyBufferOnWrite As Boolean = False

    Private ReadOnly _lock As New Object()

    'Default underlying mechanism for BlockingCollection is ConcurrentQueue<T>, which is what we want
    Private ReadOnly _Buffers As BlockingCollection(Of Byte())
    Private _maxQueueDepth As Integer = 10

    Private m_buffer As Byte() = Nothing
    Private m_offset As Integer = 0
    Private m_count As Integer = 0

    Private m_Closed As Boolean = False
    Public Overrides Sub Close()
        m_Closed = True

        'release any waiting writes
        _Buffers.CompleteAdding()
    End Sub

    Public ReadOnly Property DataAvailable As Boolean
        Get
            Return _Buffers.Count > 0
        End Get
    End Property

    Private _Length As Long = 0L
    Public Overrides ReadOnly Property Length As Long
        Get
            Return _Length
        End Get
    End Property

    Private _Position As Long = 0L
    Public Overrides Property Position As Long
        Get
            Return _Position
        End Get
        Set(value As Long)
            Throw New NotImplementedException()
        End Set
    End Property

    Public Sub New()
        Me.New(10)
    End Sub

    Public Sub New(ByVal maxQueueDepth As Integer)
        _maxQueueDepth = maxQueueDepth
        _Buffers = New BlockingCollection(Of Byte())(_maxQueueDepth)
    End Sub

    'we override the xxxxAsync functions because the default base class shares state between ReadAsync and WriteAsync, which causes a hang if both are called at once
    Public Overloads Function WriteAsync(ByVal buffer As Byte(), ByVal offset As Integer, ByVal count As Integer) As Task
        Return Task.Run(Sub() Write(buffer, offset, count))
    End Function

    'we override the xxxxAsync functions because the default base class shares state between ReadAsync and WriteAsync, which causes a hang if both are called at once
    Public Overloads Function ReadAsync(ByVal buffer As Byte(), ByVal offset As Integer, ByVal count As Integer) As Task(Of Integer)
        Return Task.Run(Function()
                            Return Read(buffer, offset, count)
                        End Function)
    End Function

    Public Overrides Sub Write(ByVal buffer As Byte(), ByVal offset As Integer, ByVal count As Integer)
        If m_Closed OrElse buffer.Length - offset < count OrElse count <= 0 Then Return

        Dim newBuffer As Byte()
        If Not CopyBufferOnWrite AndAlso offset = 0 AndAlso count = buffer.Length Then
            newBuffer = buffer
        Else
            newBuffer = New Byte(count - 1) {}
            System.Buffer.BlockCopy(buffer, offset, newBuffer, 0, count)
        End If
        If Not _Buffers.TryAdd(newBuffer, WriteTimeout) Then Throw New TimeoutException("EchoStream Write() Timeout")

        _Length += count
    End Sub

    Public Overrides Function Read(ByVal buffer As Byte(), ByVal offset As Integer, ByVal count As Integer) As Integer
        If count = 0 Then Return 0
        SyncLock _lock
            If m_count = 0 AndAlso _Buffers.Count = 0 Then
                If m_Closed Then Return -1

                If _Buffers.TryTake(m_buffer, ReadTimeout) Then
                    m_offset = 0
                    m_count = m_buffer.Length
                Else
                    Return If(m_Closed, -1, 0)
                End If
            End If

            Dim returnBytes As Integer = 0
            Do While count > 0
                If m_count = 0 Then
                    If _Buffers.TryTake(m_buffer, 0) Then
                        m_offset = 0
                        m_count = m_buffer.Length
                    Else
                        Exit Do
                    End If
                End If

                Dim bytesToCopy = If((count < m_count), count, m_count)
                System.Buffer.BlockCopy(m_buffer, m_offset, buffer, offset, bytesToCopy)
                m_offset += bytesToCopy
                m_count -= bytesToCopy
                offset += bytesToCopy
                count -= bytesToCopy

                returnBytes += bytesToCopy
            Loop

            _Position += returnBytes

            Return returnBytes
        End SyncLock
    End Function

    Public Overrides Function ReadByte() As Integer
        Dim returnValue = New Byte(0) {}
        Return If(Read(returnValue, 0, 1) <= 0, -1, returnValue(0))
    End Function

    Public Overrides Sub Flush()
    End Sub

    Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
        Throw New NotImplementedException()
    End Function

    Public Overrides Sub SetLength(value As Long)
        Throw New NotImplementedException()
    End Sub
End Class
