Imports System
Imports System.Diagnostics
Imports System.Linq
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Satori.Rtm
Imports Satori.Rtm.Client

Module Module1
    Private Const endpoint As String = "YOUR-ENDPOINT"

    Private Const appKey As String = "YOUR-APPKEY"

    ' Role and secret are optional: replace only if you need to authenticate.
    Private Const role As String = "YOUR_ROLE"

    Private Const roleSecretKey As String = "YOUR_SECRET"

    Private Const channel As String = "animals"
    ' Sample model to publish to RTM.
    ' This class represents the following raw json structure:
    ' {
    '   "who": "zebra",
    '  "where": [34.134358,-118.321506]
    ' }

    Class Animal

        <JsonProperty("who")>
        Property Who As String

        <JsonProperty("where")>
        Property Where() As Single()

        Public Overrides Function ToString() As String
            Return String.Format("[Animal: Who={0}, Where={1}]", Me.Who, String.Join(",", Where(0), Where(1)))
        End Function
    End Class

    Sub Main()
        ' Log messages from SDK to the console
        Trace.Listeners.Add(New ConsoleTraceListener)
        ' Change logging levels to increase verbosity. Default level is Warning. 
        DefaultLoggers.Dispatcher.SetLevel(Logger.LogLevel.Warning)
        DefaultLoggers.Serialization.SetLevel(Logger.LogLevel.Warning)
        DefaultLoggers.Connection.SetLevel(Logger.LogLevel.Warning)
        DefaultLoggers.Client.SetLevel(Logger.LogLevel.Warning)
        DefaultLoggers.ClientRtm.SetLevel(Logger.LogLevel.Warning)
        DefaultLoggers.ClientRtmSubscription.SetLevel(Logger.LogLevel.Warning)
        'check if the role is set to authenticate or not
        Dim toAuthenticate = (Not New String() {"YOUR_ROLE", "", Nothing}.Contains(role))
        Console.WriteLine(("RTM connection config:" & vbLf & vbTab & "endpoint='{0}'" & vbLf & vbTab & "appkey='{1}'" & vbLf & vbTab & "authenticate?={2}"), endpoint, appKey, toAuthenticate)
        Dim builder = New RtmClientBuilder(endpoint, appKey)
        If toAuthenticate Then
            builder.SetRoleSecretAuthenticator(role, roleSecretKey)
        End If

        Dim client As IRtmClient = builder.Build
        ' Hook up to client lifecycle events
        AddHandler client.OnEnterConnected, AddressOf onEnterConnected
        AddHandler client.OnError, AddressOf onError
        client.Start()
        ' We create a subscription observer object to receive callbacks
        ' for incoming messages, subscription state changes and errors. 
        ' The same observer can be shared between several subscriptions. 
        Dim observer = New SubscriptionObserver
        ' when subscription is established (confirmed by RTM)
        AddHandler observer.OnEnterSubscribed, AddressOf onEnterSubscribed
        AddHandler observer.OnSubscribeError, AddressOf onSubscribeError
        AddHandler observer.OnSubscriptionError, AddressOf onSubscriptionError
        AddHandler observer.OnSubscriptionData, AddressOf onSubscriptionData

        ' At this point, the client may not yet be connected to Satori RTM. 
        ' If the client is not connected, the SDK internally queues the subscription request and
        ' will send it once the client connects
        client.CreateSubscription(channel, SubscriptionModes.Simple, observer)
        PublishLoop(client).Wait()
    End Sub
    Sub onEnterConnected()
        Console.WriteLine("Connected to Satori RTM!")
    End Sub
    Sub onError(ByVal ex As Exception)
        Console.WriteLine(("RTM client failed: " & ex.Message))
    End Sub
    Sub onEnterSubscribed(subs As ISubscription)
        Console.WriteLine("Subscribed to the channel: " & subs.SubscriptionId)
    End Sub
    Sub onSubscribeError(subs As ISubscription, ex As Exception)
        Console.WriteLine("Subscribing failed. " & "Check channel subscribe permissions in Dev Portal. " & vbLf & ex.Message)
    End Sub
    Sub onSubscriptionError(subs As ISubscription, err As RtmSubscriptionError)
        Console.WriteLine("Subscription error " + err.Code + ": " + err.Reason)
    End Sub
    Sub onSubscriptionData(subs As ISubscription, data As RtmSubscriptionData)
        For Each jToken As JToken In data.Messages
            Try
                Dim msg As Animal = jToken.ToObject(Of Animal)
                Console.WriteLine("Got animal {0}: {1}", msg.Who, jToken)
            Catch ex As Exception
                Console.WriteLine("Failed to handle the incoming message: {0}", ex.Message)
            End Try
        Next
    End Sub
    Async Function PublishLoop(ByVal client As IRtmClient) As Task
        ' Publish messages every 2 seconds
        Dim random = New Random()

        While True
            Try
                Dim message = New Animal()
                message.Who = "zebra"
                message.Where = (New Single() {
                    34.134358 + random.NextDouble() / 100,
                    -118.321506 + random.NextDouble() / 100})
                ' At this point, the client may not yet be connected to Satori RTM.
                ' If the client is not connected, the SDK internally queues the publish request and
                ' will send it once the client connects
                Dim reply As RtmPublishReply
                reply = Await client.Publish(channel, message, Ack.Yes)
                Console.WriteLine("Animal is published: {0}", message)
            Catch ex As PduException
                Console.WriteLine("Failed to publish. RTM replied with the error {0}: {1}", ex.Error.Code, ex.Error.Reason)
            Catch ex As Exception
                Console.WriteLine(("Failed to publish: " + ex.Message))
            End Try

            Await Task.Delay(millisecondsDelay:=2000)

        End While
    End Function
End Module
