### Local Web Server for ZPL Printers

- **Tested on**: Xprinter XP-420B and ZDesigner GK888t
- **Purpose**: Send ZPL commands as web requests to this web server, which forwards them to a printer connected through USB.
- **Installer**: Located at `releases/WebToZpl.msi`

### Instructions

1. **Install the Server**:
    - Download and install `WebToZpl.msi` from `releases/WebToZpl.msi`.

2. **Run the Server**:
    - Run the server as an administrator.
    - Configure the IP address (default is your local machine IP address) and specify the port. You can also leave these as default.

3. **Select the Printer and Start**:
    - Select the printer and click "Start".

4. **Send ZPL Commands**:
    - Send a GET request to `http://<ip address>:<port>?zpl=<your zpl command as URL encoded string>`.
    - Example:
        - Your ZPL command: `^XA^FO50,50^A0N,50,50^FDHello, World!^FS^XZ`
        - URL encoded string: `%5EXA%5EFO50%2C50%5EA0N%2C50%2C50%5EFDHello%2C%20World%21%5EFS%5EXZ`
        - Open this link in your browser: `http://localhost:2024/?zpl=%5EXA%5EFO50%2C50%5EA0N%2C50%2C50%5EFDHello%2C%20World%21%5EFS%5EXZ`

### Notes

- Ensure the server is running as an administrator to access USB-connected printers.