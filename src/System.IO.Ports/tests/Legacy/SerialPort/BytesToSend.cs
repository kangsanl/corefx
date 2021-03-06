// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO.Ports;
using System.IO.PortsTests;
using System.Threading.Tasks;
using Legacy.Support;
using Xunit;

public class BytesToWrite_Property : PortsTest
{
    static readonly int DEFAULT_NUM_RND_BYTES = 8;

    //The default number of chars to write with when testing timeout with Write(char[], int, int)
    static readonly int DEFAULT_WRITE_CHAR_ARRAY_SIZE = 8;

    //The default number of bytes to write with when testing timeout with Write(byte[], int, int)
    static readonly int DEFAULT_WRITE_BYTE_ARRAY_SIZE = 8;

    //The default string to write with when testing timeout with Write(str)
    static readonly string DEFAULT_STRING_TO_WRITE = "TEST";

    //Delegate to start asynchronous write on the SerialPort com with buffer of size bufferLength
    delegate int WriteMethodDelegate(SerialPort com, int bufferSize);

    #region Test Cases

    [ConditionalFact(nameof(HasOneSerialPort))]
    public void BytesToWrite_Default()
    {
        using (SerialPort com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        {
            SerialPortProperties serPortProp = new SerialPortProperties();

            Debug.WriteLine("Verifying default BytesToWrite");

            serPortProp.SetAllPropertiesToOpenDefaults();
            serPortProp.SetProperty("PortName", TCSupport.LocalMachineSerialInfo.FirstAvailablePortName);
            com1.Open();
            serPortProp.VerifyPropertiesAndPrint(com1);
        }
    }

    [ConditionalFact(nameof(HasNullModem))]
    public void BytesToWrite_Write_byte_int_int()
    {
        Debug.WriteLine("Verifying BytesToWrite with Write(byte[] buffer, int offset, int count)");
        VerifyBytesToWrite(Write_byte_int_int, DEFAULT_NUM_RND_BYTES, false);
    }

    [ConditionalFact(nameof(HasNullModem))]
    public void BytesToWrite_Write_char_int_int()
    {
        Debug.WriteLine("Verifying BytesToWrite with Write(char[] buffer, int offset, int count)");
        VerifyBytesToWrite(Write_char_int_int, DEFAULT_NUM_RND_BYTES, false);
    }

    [ConditionalFact(nameof(HasNullModem))]
    public void BytesToWrite_Write_str()
    {
        Debug.WriteLine("Verifying BytesToWrite with WriteChar()");
        VerifyBytesToWrite(Write_str, 1, false);
    }

    [ConditionalFact(nameof(HasNullModem))]
    public void BytesToWrite_WriteLine()
    {
        Debug.WriteLine("Verifying BytesToWrite with WriteLine()");
        VerifyBytesToWrite(WriteLine, DEFAULT_NUM_RND_BYTES, true);
    }
    #endregion

    #region Verification for Test Cases
    private void VerifyBytesToWrite(WriteMethodDelegate writeMethod, int bufferSize, bool sendNewLine)
    {
        using (SerialPort com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        using (SerialPort com2 = new SerialPort(TCSupport.LocalMachineSerialInfo.SecondAvailablePortName))
        {
            SerialPortProperties serPortProp = new SerialPortProperties();
            int expectedBytesToWrite;
            int actualBytesToWrite;

            serPortProp.SetAllPropertiesToOpenDefaults();
            serPortProp.SetProperty("PortName", TCSupport.LocalMachineSerialInfo.FirstAvailablePortName);
            serPortProp.SetProperty("Handshake", Handshake.RequestToSend);
            serPortProp.SetProperty("WriteTimeout", 500);

            com1.Open();
            com1.WriteTimeout = 500;
            com1.Handshake = Handshake.RequestToSend;

            Task<int> task = Task.Run(() => writeMethod(com1, bufferSize));

            System.Threading.Thread.Sleep(200);

            actualBytesToWrite = com1.BytesToWrite;
            expectedBytesToWrite = task.Result;

            com2.Open();
            com2.RtsEnable = true;

            Assert.Equal(expectedBytesToWrite, actualBytesToWrite);

            com2.RtsEnable = false;
            serPortProp.VerifyPropertiesAndPrint(com1);

        }
    }


    private int Write_byte_int_int(SerialPort com, int bufferSize)
    {
        try
        {
            com.Write(new byte[DEFAULT_WRITE_BYTE_ARRAY_SIZE], 0, DEFAULT_WRITE_BYTE_ARRAY_SIZE);
        }
        catch (TimeoutException)
        {
        }
        return bufferSize;
    }


    private int Write_char_int_int(SerialPort com, int bufferSize)
    {
        char[] charsToWrite = new char[DEFAULT_WRITE_CHAR_ARRAY_SIZE];

        try
        {
            com.Write(charsToWrite, 0, charsToWrite.Length);
        }
        catch (TimeoutException)
        {
        }
        return com.Encoding.GetByteCount(charsToWrite);
    }


    private int Write_str(SerialPort com, int bufferSize)
    {
        try
        {
            com.Write(DEFAULT_STRING_TO_WRITE);
        }
        catch (TimeoutException)
        {
        }
        return com.Encoding.GetByteCount(DEFAULT_STRING_TO_WRITE.ToCharArray());
    }


    private int WriteLine(SerialPort com, int bufferSize)
    {
        try
        {
            com.WriteLine(DEFAULT_STRING_TO_WRITE);
        }
        catch (TimeoutException)
        {
        }
        return com.Encoding.GetByteCount(DEFAULT_STRING_TO_WRITE.ToCharArray()) + com.Encoding.GetByteCount(com.NewLine.ToCharArray());
    }
    #endregion
}
