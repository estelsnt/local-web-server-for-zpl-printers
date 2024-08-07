Imports System
Imports System.Net
Imports System.Threading
Imports System.Net.NetworkInformation
Imports System.Drawing.Printing
Imports System.Web
Imports System.IO
Imports System.Runtime.InteropServices
Public Class Form1
    Private listener As HttpListener

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        PopulatePrintersComboBox()
        Dim ipv4Address As String = GetPrimaryIPv4Address()
        If Not String.IsNullOrEmpty(ipv4Address) Then
            TextBox2.Text = ipv4Address
        Else
            Console.WriteLine("Please connect to a network.")
        End If
    End Sub

    Function GetPrimaryIPv4Address() As String
        Dim networkInterfaces As NetworkInterface() = NetworkInterface.GetAllNetworkInterfaces()

        For Each networkInterface As NetworkInterface In networkInterfaces
            ' Filter out loopback, tunnel, and other non-physical interfaces
            If networkInterface.NetworkInterfaceType = NetworkInterfaceType.Ethernet OrElse
               networkInterface.NetworkInterfaceType = NetworkInterfaceType.Wireless80211 Then
                Dim ipProperties As IPInterfaceProperties = networkInterface.GetIPProperties()
                Dim unicastAddresses As UnicastIPAddressInformationCollection = ipProperties.UnicastAddresses

                For Each unicastAddress As UnicastIPAddressInformation In unicastAddresses
                    If unicastAddress.Address.AddressFamily = Net.Sockets.AddressFamily.InterNetwork AndAlso
                       unicastAddress.IsDnsEligible AndAlso
                       unicastAddress.AddressPreferredLifetime <> UInt32.MaxValue Then
                        Return unicastAddress.Address.ToString()
                    End If
                Next
            End If
        Next

        Return Nothing
    End Function

    Private Sub PopulatePrintersComboBox()
        ' Clear the existing items in the ComboBox
        ComboBox1.Items.Clear()

        ' Get the list of installed printers
        Dim printers As String() = PrinterSettings.InstalledPrinters.Cast(Of String)().ToArray()

        ' Add the printers to the ComboBox
        If printers.Length > 0 Then
            ' Add the printers to the ComboBox
            ComboBox1.Items.AddRange(printers)
        Else
            ' Show message box if no printers are found
            Label5.Text = "No printer found"
        End If
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        If ComboBox1.Text = "" Then
            MessageBox.Show("Please select printer")
            Return
        End If

        If TextBox1.Text = "" Then
            MessageBox.Show("Please specify Port number")
            Return
        End If

        If listener IsNot Nothing AndAlso listener.IsListening Then
            MessageBox.Show("Server is already running.")
            Return
        End If

        Dim port As Integer = Convert.ToInt32(TextBox1.Text)
        Dim prefix As String = $"http://+:{port}/"
        listener = New HttpListener()
        listener.Prefixes.Add(prefix)

        Try
            listener.Start()
            MessageBox.Show($"Server started. Listening on port {port}.")
            ThreadPool.QueueUserWorkItem(AddressOf ListenForRequests)
            Label5.Text = "Server is running"
        Catch ex As Exception
            MessageBox.Show($"Error starting server: {ex.Message}")
            Label5.Text = ""
        End Try
    End Sub

    Private Sub ListenForRequests(state As Object)
        While listener.IsListening
            Dim context As HttpListenerContext = listener.GetContext()

            ' Process the request
            Dim request As HttpListenerRequest = context.Request
            Dim response As HttpListenerResponse = context.Response

            ' Add CORS headers to allow requests from any origin
            response.Headers.Add("Access-Control-Allow-Origin", "*")
            response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS")
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type")

            ' Check if the request is a preflight CORS request (OPTIONS method)
            If request.HttpMethod = "OPTIONS" Then
                response.StatusCode = 200
                response.OutputStream.Close()
                Continue While
            End If

            ' Check if the request is a POST
            If request.HttpMethod = "POST" Then
                ' Read the POST data
                Using reader As New System.IO.StreamReader(request.InputStream, request.ContentEncoding)
                    Dim postData As String = reader.ReadToEnd()

                    ' Respond with a simple message
                    Dim responseString As String = "Received POST request with data: " & postData

                    Dim buffer As Byte() = System.Text.Encoding.UTF8.GetBytes(responseString)
                    Me.Invoke(Sub() handlePrinting(postData))
                    ' Set Content-Type header
                    response.ContentType = "text/plain"

                    ' Write response
                    Dim responseText As Byte() = System.Text.Encoding.UTF8.GetBytes("ZPL Print Server")
                    response.ContentLength64 = responseText.Length
                    response.OutputStream.Write(responseText, 0, responseText.Length)
                End Using
            End If

            ' Check if the request is a GET
            If request.HttpMethod = "GET" Then
                ' Get the query parameters from the request URL
                Dim queryData As String = request.Url.Query
                Dim postData As String = ""

                ' Extract the data parameter from the query string
                If queryData.StartsWith("?zpl=") Then
                    postData = queryData.Substring(5) ' Remove "?data=" prefix
                    postData = HttpUtility.UrlDecode(postData) ' Decode URL-encoded data
                    Me.Invoke(Sub() handlePrintingGET(postData))
                End If

                ' Respond with a simple message
                Dim responseString As String = "Received GET request with data: " & postData

                ' Set Content-Type header
                response.ContentType = "text/plain"

                ' Write response
                Dim responseText As Byte() = System.Text.Encoding.UTF8.GetBytes(responseString)
                response.ContentLength64 = responseText.Length
                response.OutputStream.Write(responseText, 0, responseText.Length)
            End If


            ' Close the response stream
            response.OutputStream.Close()
        End While
    End Sub

    Private Sub handlePrintingGET(zlps As String)
        ' begin printing
        Dim stringWithoutNewlines As String = zlps.Replace(Environment.NewLine, "")
        PrintZPL(HttpUtility.UrlDecode(stringWithoutNewlines))
    End Sub
    Private Sub handlePrinting(zlps As String)
        Dim inputData As String = zlps

        Dim zplData As String = ""

        ' Find the index of "zpl"
        Dim index As Integer = inputData.IndexOf("zpl") + 1


        If index <> -1 Then
            ' Get the substring after "zpl"
            zplData = inputData.Substring(index + 3) ' +3 to account for "zpl" length
            ' Remove any leading whitespace or newline characters
            zplData = zplData.TrimStart()
            Dim ending As Integer = zplData.IndexOf("------WebKitFormBoundary")
            zplData = zplData.Substring(0, ending).Trim()

        Else
            Console.WriteLine("No 'zpl' found in the input data.")
        End If

        Dim decodedUri As String = HttpUtility.UrlDecode(zplData)

        ' begin printing
        PrintZPL(decodedUri)
    End Sub
    Private Sub PrintZPL(ByVal zplCommands As String)
        Try
            Dim printerName As String = ComboBox1.SelectedItem.ToString() ' Replace with your printer name
            RawPrinterHelper.SendStringToPrinter(printerName, zplCommands)
        Catch ex As Exception
            MessageBox.Show("Printer error")
        End Try
    End Sub

    Private Sub TextBox1_KeyPress(sender As Object, e As KeyPressEventArgs) Handles TextBox1.KeyPress
        If Not Char.IsDigit(e.KeyChar) AndAlso Not Char.IsControl(e.KeyChar) Then
            ' If not a numeric digit or a control key, ignore the input by setting Handled to true
            e.Handled = True
        End If
    End Sub
End Class


Public Class RawPrinterHelper
    Public Shared Function SendStringToPrinter(ByVal printerName As String, ByVal zplString As String) As Boolean
        Dim pBytes As IntPtr
        Dim dwCount As Int32

        ' Calculate the number of bytes to send.
        dwCount = zplString.Length

        ' Convert the string to an array of bytes.
        pBytes = Marshal.StringToCoTaskMemAnsi(zplString)

        ' Send the converted string to the printer.
        SendBytesToPrinter(printerName, pBytes, dwCount)

        Marshal.FreeCoTaskMem(pBytes)
        Return True
    End Function

    Private Shared Function SendBytesToPrinter(ByVal printerName As String, ByVal pBytes As IntPtr, ByVal dwCount As Int32) As Boolean
        Dim hPrinter As IntPtr
        Dim di As DOCINFOA
        Dim dwWritten As Int32
        Dim bSuccess As Boolean

        ' Initialize the printer structure.
        di = New DOCINFOA
        di.pDocName = "ZPL Document"
        di.pDataType = "RAW"

        ' Open the printer.
        If OpenPrinter(printerName, hPrinter, IntPtr.Zero) Then
            ' Start a document.
            If StartDocPrinter(hPrinter, 1, di) Then
                ' Start a page.
                If StartPagePrinter(hPrinter) Then
                    ' Write the bytes.
                    bSuccess = WritePrinter(hPrinter, pBytes, dwCount, dwWritten)
                    EndPagePrinter(hPrinter)

                End If
                EndDocPrinter(hPrinter)
            End If
            ClosePrinter(hPrinter)
        End If

        ' Return success status.
        If bSuccess = False Then
            Throw New Exception("Failed to send ZPL string to printer.")
        End If

        Return bSuccess
    End Function

    <DllImport("winspool.drv", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Shared Function OpenPrinter(ByVal szPrinter As String, ByRef hPrinter As IntPtr, ByVal pd As IntPtr) As Boolean
    End Function

    <DllImport("winspool.drv", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Shared Function ClosePrinter(ByVal hPrinter As IntPtr) As Boolean
    End Function

    <DllImport("winspool.drv", EntryPoint:="StartDocPrinterA", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Shared Function StartDocPrinter(ByVal hPrinter As IntPtr, ByVal level As Int32, ByRef pDI As DOCINFOA) As Boolean
    End Function

    <DllImport("winspool.drv", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Shared Function EndDocPrinter(ByVal hPrinter As IntPtr) As Boolean
    End Function

    <DllImport("winspool.drv", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Shared Function StartPagePrinter(ByVal hPrinter As IntPtr) As Boolean
    End Function

    <DllImport("winspool.drv", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Shared Function EndPagePrinter(ByVal hPrinter As IntPtr) As Boolean
    End Function

    <DllImport("winspool.drv", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Shared Function WritePrinter(ByVal hPrinter As IntPtr, ByVal pBytes As IntPtr, ByVal dwCount As Int32, ByRef dwWritten As Int32) As Boolean
    End Function

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Ansi)>
    Public Structure DOCINFOA
        <MarshalAs(UnmanagedType.LPStr)> Public pDocName As String
        <MarshalAs(UnmanagedType.LPStr)> Public pOutputFile As String
        <MarshalAs(UnmanagedType.LPStr)> Public pDataType As String
    End Structure
End Class
