// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Ports;
using System.Diagnostics;
using System.IO.PortsTests;
using Legacy.Support;
using Xunit;

public class Read_byte_int_int_Generic : PortsTest
{
    //Set bounds fore random timeout values.
    //If the min is to low read will not timeout accurately and the testcase will fail
    public static readonly int minRandomTimeout = 250;

    //If the max is to large then the testcase will take forever to run
    public static readonly int maxRandomTimeout = 2000;

    //If the percentage difference between the expected timeout and the actual timeout
    //found through Stopwatch is greater then 10% then the timeout value was not correctly
    //to the read method and the testcase fails.
    public static readonly double maxPercentageDifference = .15;

    //The number of random bytes to receive for parity testing
    public static readonly int numRndBytesPairty = 8;

    //The number of characters to read at a time for parity testing
    public static readonly int numBytesReadPairty = 2;

    //The number of random bytes to receive for BytesToRead testing
    public static readonly int numRndBytesToRead = 16;

    //When we test Read and do not care about actually reading anything we must still
    //create an byte array to pass into the method the following is the size of the 
    //byte array used in this situation
    public static readonly int defaultByteArraySize = 1;
    public static readonly int NUM_TRYS = 5;

    #region Test Cases

    [Fact]
    public void ReadWithoutOpen()
    {
        using (SerialPort com = new SerialPort())
        {
            Debug.WriteLine("Verifying read method throws exception without a call to Open()");

            VerifyReadException(com, typeof(InvalidOperationException));
        }
    }

    [ConditionalFact(nameof(HasOneSerialPort))]
    public void ReadAfterFailedOpen()
    {
        using (SerialPort com = new SerialPort("BAD_PORT_NAME"))
        {
            Debug.WriteLine("Verifying read method throws exception with a failed call to Open()");

            //Since the PortName is set to a bad port name Open will thrown an exception
            //however we don't care what it is since we are verifying a read method
            Assert.ThrowsAny<Exception>(() => com.Open());
            VerifyReadException(com, typeof(InvalidOperationException));
        }
    }


    [ConditionalFact(nameof(HasOneSerialPort))]
    public void ReadAfterClose()
    {
        using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        {
            Debug.WriteLine("Verifying read method throws exception after a call to Cloes()");
            com.Open();
            com.Close();

            VerifyReadException(com, typeof(InvalidOperationException));
        }
    }

    [OuterLoop("Slow test")]
    [ConditionalFact(nameof(HasOneSerialPort))]
    public void Timeout()
    {
        using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        {
            Random rndGen = new Random();

            com.ReadTimeout = rndGen.Next(minRandomTimeout, maxRandomTimeout);
            Debug.WriteLine("Verifying ReadTimeout={0}", com.ReadTimeout);
            com.Open();

            VerifyTimeout(com);
        }
    }

    [OuterLoop("Slow test")]
    [ConditionalFact(nameof(HasOneSerialPort))]
    public void SuccessiveReadTimeoutNoData()
    {
        using (SerialPort com = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        {
            Random rndGen = new Random();

            com.ReadTimeout = rndGen.Next(minRandomTimeout, maxRandomTimeout);
            //		com.Encoding = new System.Text.UTF7Encoding();
            com.Encoding = System.Text.Encoding.Unicode;

            Debug.WriteLine("Verifying ReadTimeout={0} with successive call to read method and no data", com.ReadTimeout);
            com.Open();

            Assert.Throws<TimeoutException>(() => com.Read(new byte[defaultByteArraySize], 0, defaultByteArraySize));

            VerifyTimeout(com);
        }
    }


    [ConditionalFact(nameof(HasNullModem))]
    public void SuccessiveReadTimeoutSomeData()
    {
        using (SerialPort com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        {
            Random rndGen = new Random();
            System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(WriteToCom1));

            com1.ReadTimeout = rndGen.Next(minRandomTimeout, maxRandomTimeout);
            com1.Encoding = new System.Text.UTF8Encoding();

            Debug.WriteLine("Verifying ReadTimeout={0} with successive call to read method and some data being received in the first call", com1.ReadTimeout);
            com1.Open();

            //Call WriteToCom1 asynchronously this will write to com1 some time before the following call 
            //to a read method times out
            t.Start();

            try
            {
                com1.Read(new byte[defaultByteArraySize], 0, defaultByteArraySize);
            }
            catch (TimeoutException)
            {
            }

            //Wait for the thread to finish
            while (t.IsAlive)
                System.Threading.Thread.Sleep(50);

            //Make sure there is no bytes in the buffer so the next call to read will timeout
            com1.DiscardInBuffer();
            VerifyTimeout(com1);
        }
    }


    private void WriteToCom1()
    {
        using (SerialPort com2 = new SerialPort(TCSupport.LocalMachineSerialInfo.SecondAvailablePortName))
        {
            Random rndGen = new Random();
            byte[] xmitBuffer = new byte[1];
            int sleepPeriod = rndGen.Next(minRandomTimeout, maxRandomTimeout / 2);

            //Sleep some random period with of a maximum duration of half the largest possible timeout value for a read method on COM1
            System.Threading.Thread.Sleep(sleepPeriod);

            com2.Open();
            com2.Write(xmitBuffer, 0, xmitBuffer.Length);

            if (com2.IsOpen)
                com2.Close();
        }
    }


    public void DefaultParityReplaceByte()
    {
        VerifyParityReplaceByte(-1, numRndBytesPairty - 2);
    }


    [ConditionalFact(nameof(HasNullModem))]
    public void NoParityReplaceByte()
    {
        Random rndGen = new Random();

        //		if(!VerifyParityReplaceByte((int)'\0', rndGen.Next(0, numRndBytesPairty - 1), new System.Text.UTF7Encoding())){
        VerifyParityReplaceByte((int)'\0', rndGen.Next(0, numRndBytesPairty - 1), System.Text.Encoding.UTF32);
    }


    [ConditionalFact(nameof(HasNullModem))]
    public void RNDParityReplaceByte()
    {
        Random rndGen = new Random();

        VerifyParityReplaceByte(rndGen.Next(0, 128), 0, new System.Text.UTF8Encoding());
    }


    [ConditionalFact(nameof(HasNullModem))]
    public void ParityErrorOnLastByte()
    {
        using (SerialPort com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        using (SerialPort com2 = new SerialPort(TCSupport.LocalMachineSerialInfo.SecondAvailablePortName))
        {
            Random rndGen = new Random(15);
            byte[] bytesToWrite = new byte[numRndBytesPairty];
            byte[] expectedBytes = new byte[numRndBytesPairty];
            byte[] actualBytes = new byte[numRndBytesPairty + 1];
            int waitTime;

            /* 1 Additional character gets added to the input buffer when the parity error occurs on the last byte of a stream
                 We are verifying that besides this everything gets read in correctly. See NDP Whidbey: 24216 for more info on this */
            Debug.WriteLine("Verifying default ParityReplace byte with a parity errro on the last byte");

            //Genrate random characters without an parity error
            for (int i = 0; i < bytesToWrite.Length; i++)
            {
                byte randByte = (byte)rndGen.Next(0, 128);

                bytesToWrite[i] = randByte;
                expectedBytes[i] = randByte;
            }

            bytesToWrite[bytesToWrite.Length - 1] = (byte)(bytesToWrite[bytesToWrite.Length - 1] | 0x80);
                //Create a parity error on the last byte
            expectedBytes[expectedBytes.Length - 1] = com1.ParityReplace;
                // Set the last expected byte to be the ParityReplace Byte

            com1.Parity = Parity.Space;
            com1.DataBits = 7;
            com1.ReadTimeout = 250;

            com1.Open();
            com2.Open();

            com2.Write(bytesToWrite, 0, bytesToWrite.Length);

            waitTime = 0;

            while (bytesToWrite.Length + 1 > com1.BytesToRead && waitTime < 500)
            {
                System.Threading.Thread.Sleep(50);
                waitTime += 50;
            }

            com1.Read(actualBytes, 0, actualBytes.Length);

            //Compare the chars that were written with the ones we expected to read
            Assert.Equal(expectedBytes, actualBytes);

            if (1 < com1.BytesToRead)
            {
                Fail("ERROR!!!: Expected BytesToRead=0 actual={0}", com1.BytesToRead);
                Debug.WriteLine("ByteRead={0}, {1}", com1.ReadByte(), bytesToWrite[bytesToWrite.Length - 1]);
            }

            bytesToWrite[bytesToWrite.Length - 1] = (byte)(bytesToWrite[bytesToWrite.Length - 1] & 0x7F);
                //Clear the parity error on the last byte
            expectedBytes[expectedBytes.Length - 1] = bytesToWrite[bytesToWrite.Length - 1];
            VerifyRead(com1, com2, bytesToWrite, expectedBytes, expectedBytes.Length / 2);
        }
    }

    [ConditionalFact(nameof(HasNullModem))]
    public void BytesToRead_RND_Buffer_Size()
    {
        Random rndGen = new Random(-55);

        VerifyBytesToRead(rndGen.Next(1, 2 * numRndBytesToRead));
    }

    [ConditionalFact(nameof(HasNullModem))]
    public void BytesToRead_1_Buffer_Size()
    {
        //		if(!VerifyBytesToRead(1, new System.Text.UTF7Encoding())){
        VerifyBytesToRead(1, System.Text.Encoding.Unicode);
    }

    [ConditionalFact(nameof(HasNullModem))]
    public void BytesToRead_Equal_Buffer_Size()
    {
        Random rndGen = new Random(-55);

        VerifyBytesToRead(numRndBytesToRead, new System.Text.UTF8Encoding());
    }
    #endregion

    #region Verification for Test Cases
    private void VerifyTimeout(SerialPort com)
    {
        Stopwatch timer = new Stopwatch();
        int expectedTime = com.ReadTimeout;
        int actualTime = 0;
        double percentageDifference;

        //Warm up read method
        Assert.Throws<TimeoutException>(() => com.Read(new byte[defaultByteArraySize], 0, defaultByteArraySize));

        System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;

        for (int i = 0; i < NUM_TRYS; i++)
        {
            timer.Start();
            Assert.Throws<TimeoutException>(() => com.Read(new byte[defaultByteArraySize], 0, defaultByteArraySize));

            timer.Stop();
            actualTime += (int)timer.ElapsedMilliseconds;
            timer.Reset();
        }

        System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Normal;
        actualTime /= NUM_TRYS;
        percentageDifference = Math.Abs((expectedTime - actualTime) / (double)expectedTime);

        //Verify that the percentage difference between the expected and actual timeout is less then maxPercentageDifference
        if (maxPercentageDifference < percentageDifference)
        {
            Fail("ERROR!!!: The read method timedout in {0} expected {1} percentage difference: {2}", actualTime, expectedTime, percentageDifference);
        }
    }


    private void VerifyReadException(SerialPort com, Type expectedException)
    {
        Assert.Throws(expectedException, () => com.Read(new byte[defaultByteArraySize], 0, defaultByteArraySize));
    }

    public void VerifyParityReplaceByte(int parityReplace, int parityErrorIndex)
    {
        VerifyParityReplaceByte(parityReplace, parityErrorIndex, new System.Text.ASCIIEncoding());
    }

public void VerifyParityReplaceByte(int parityReplace, int parityErrorIndex, System.Text.Encoding encoding)
    {
        using (SerialPort com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        using (SerialPort com2 = new SerialPort(TCSupport.LocalMachineSerialInfo.SecondAvailablePortName))
        {
            Random rndGen = new Random(-55);
            byte[] bytesToWrite = new byte[numRndBytesPairty];
            byte[] expectedBytes = new byte[numRndBytesPairty];
            byte expectedByte;

            //Genrate random characters without an parity error
            for (int i = 0; i < bytesToWrite.Length; i++)
            {
                byte randByte = (byte)rndGen.Next(0, 128);

                bytesToWrite[i] = randByte;
                expectedBytes[i] = randByte;
            }

            if (-1 == parityReplace)
            {
                //If parityReplace is -1 and we should just use the default value
                expectedByte = com1.ParityReplace;
            }
            else if ('\0' == parityReplace)
            {
                //If parityReplace is the null charachater and parity replacement should not occur
                com1.ParityReplace = (byte)parityReplace;
                expectedByte = bytesToWrite[parityErrorIndex];
            }
            else
            {
                //Else parityReplace was set to a value and we should expect this value to be returned on a parity error
                com1.ParityReplace = (byte)parityReplace;
                expectedByte = (byte)parityReplace;
            }

            //Create an parity error by setting the highest order bit to true
            bytesToWrite[parityErrorIndex] = (byte)(bytesToWrite[parityErrorIndex] | 0x80);
            expectedBytes[parityErrorIndex] = (byte)expectedByte;

            Debug.WriteLine("Verifying ParityReplace={0} with an ParityError at: {1} ", com1.ParityReplace,
                parityErrorIndex);

            com1.Parity = Parity.Space;
            com1.DataBits = 7;
            com1.Encoding = encoding;

            com1.Open();
            com2.Open();

            VerifyRead(com1, com2, bytesToWrite, expectedBytes, numBytesReadPairty);
        }
    }

    private void VerifyBytesToRead(int numBytesRead)
    {
        VerifyBytesToRead(numBytesRead, new System.Text.ASCIIEncoding());
    }

    private void VerifyBytesToRead(int numBytesRead, System.Text.Encoding encoding)
    {
        using (SerialPort com1 = new SerialPort(TCSupport.LocalMachineSerialInfo.FirstAvailablePortName))
        using (SerialPort com2 = new SerialPort(TCSupport.LocalMachineSerialInfo.SecondAvailablePortName))
        {
            Random rndGen = new Random(-55);
            byte[] bytesToWrite = new byte[numRndBytesToRead];

            //Genrate random characters 
            for (int i = 0; i < bytesToWrite.Length; i++)
            {
                byte randByte = (byte)rndGen.Next(0, 256);

                bytesToWrite[i] = randByte;
            }

            Debug.WriteLine("Verifying BytesToRead with a buffer of: {0} ", numBytesRead);

            com1.Encoding = encoding;

            com1.Open();
            com2.Open();

            VerifyRead(com1, com2, bytesToWrite, bytesToWrite, numBytesRead);
        }
    }

    private void VerifyRead(SerialPort com1, SerialPort com2, byte[] bytesToWrite, byte[] expectedBytes, int rcvBufferSize)
    {
        byte[] rcvBuffer = new byte[rcvBufferSize];
        byte[] buffer = new byte[bytesToWrite.Length];
        int bytesRead, totalBytesRead;
        int bytesToRead;
        int waitTime = 0;

        com2.Write(bytesToWrite, 0, bytesToWrite.Length);
        com1.ReadTimeout = 250;

        while (com1.BytesToRead < bytesToWrite.Length && waitTime < 500)
        {
            System.Threading.Thread.Sleep(50);
            waitTime += 50;
        }

        totalBytesRead = 0;
        bytesToRead = com1.BytesToRead;

        while (true)
        {
            try
            {
                bytesRead = com1.Read(rcvBuffer, 0, rcvBufferSize);
            }
            catch (TimeoutException)
            {
                break;
            }

            //While their are more characters to be read
            if ((bytesToRead > bytesRead && rcvBufferSize != bytesRead) ||
                (bytesToRead <= bytesRead && bytesRead != bytesToRead))
            {
                //If we have not read all of the characters that we should have
                Fail("ERROR!!!: Read did not return all of the characters that were in SerialPort buffer");
            }

            if (bytesToWrite.Length < totalBytesRead + bytesRead)
            {
                //If we have read in more characters then we expect
                Fail("ERROR!!!: We have received more characters then were sent");
            }

            Array.Copy(rcvBuffer, 0, buffer, totalBytesRead, bytesRead);
            totalBytesRead += bytesRead;

            if (bytesToWrite.Length - totalBytesRead != com1.BytesToRead)
            {
                Fail("ERROR!!!: Expected BytesToRead={0} actual={1}", bytesToWrite.Length - totalBytesRead,
                    com1.BytesToRead);
            }

            bytesToRead = com1.BytesToRead;
        }

        //Compare the bytes that were written with the ones we expected to read
        for (int i = 0; i < bytesToWrite.Length; i++)
        {
            if (expectedBytes[i] != buffer[i])
            {
                Fail("ERROR!!!: Expected to read {0}  actual read  {1}", expectedBytes[i], buffer[i]);
            }
        }
    }

    #endregion
}
