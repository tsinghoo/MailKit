﻿//
// SmtpClientTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;
using MimeKit.IO;
using MimeKit.Utils;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpClientTests
	{
		class MyProgress : ITransferProgress
		{
			public long BytesTransferred;
			public long TotalSize;

			public void Report (long bytesTransferred, long totalSize)
			{
				BytesTransferred = bytesTransferred;
				TotalSize = totalSize;
			}

			public void Report (long bytesTransferred)
			{
				BytesTransferred = bytesTransferred;
			}
		}

		MimeMessage CreateSimpleMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient Name", "recipient@example.com"));
			message.Subject = "This is a test...";

			message.Body = new TextPart ("plain") {
				Text = "This is the message body."
			};

			return message;
		}

		MimeMessage CreateBinaryMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient Name", "recipient@example.com"));
			message.Subject = "This is a test...";

			message.Body = new TextPart ("plain") {
				Text = "This is the message body with some unicode unicode: ☮ ☯",
				ContentTransferEncoding = ContentEncoding.Binary
			};

			return message;
		}

		MimeMessage CreateEightBitMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient Name", "recipient@example.com"));
			message.Subject = "This is a test...";

			message.Body = new TextPart ("plain") {
				Text = "This is the message body with some unicode unicode: ☮ ☯"
			};

			return message;
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			using (var client = new SmtpClient ()) {
				var credentials = new NetworkCredential ("username", "password");
				var message = CreateSimpleMessage ();
				var sender = message.From.Mailboxes.FirstOrDefault ();
				var recipients = message.To.Mailboxes.ToList ();
				var options = FormatOptions.Default;
				var empty = new MailboxAddress[0];

				// ReplayConnect
				Assert.Throws<ArgumentNullException> (() => client.ReplayConnect (null, Stream.Null));
				Assert.Throws<ArgumentNullException> (() => client.ReplayConnect ("host", null));
				Assert.Throws<ArgumentNullException> (async () => await client.ReplayConnectAsync (null, Stream.Null));
				Assert.Throws<ArgumentNullException> (async () => await client.ReplayConnectAsync ("host", null));

				// Connect
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Uri) null));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync ((Uri) null));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 25, false));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, 25, false));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 25, false));
				Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 25, false));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, false));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, false));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 25, SecureSocketOptions.None));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, 25, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 25, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 25, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, SecureSocketOptions.None));

				Assert.Throws<ArgumentNullException> (() => client.Connect (null, "host", 25, SecureSocketOptions.None));
				using (var socket = new Socket (SocketType.Stream, ProtocolType.Tcp))
					Assert.Throws<ArgumentException> (() => client.Connect (socket, "host", 25, SecureSocketOptions.None));

				using (var socket = Connect ("www.gmail.com", 80)) {
					Assert.Throws<ArgumentNullException> (() => client.Connect (null, "host", 25, SecureSocketOptions.None));
					Assert.Throws<ArgumentNullException> (() => client.Connect (socket, null, 25, SecureSocketOptions.None));
					Assert.Throws<ArgumentException> (() => client.Connect (socket, string.Empty, 25, SecureSocketOptions.None));
					Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect (socket, "host", -1, SecureSocketOptions.None));
					Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, "host", 25, SecureSocketOptions.None));
					Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (socket, null, 25, SecureSocketOptions.None));
					Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (socket, string.Empty, 25, SecureSocketOptions.None));
					Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync (socket, "host", -1, SecureSocketOptions.None));
				}

				// Authenticate
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ((SaslMechanism) null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync ((SaslMechanism) null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ((ICredentials) null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync ((ICredentials) null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ("username", null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync ("username", null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, credentials));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, credentials));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, "username", null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, "username", null));

				// Send
				Assert.Throws<ArgumentNullException> (() => client.Send (null));

				Assert.Throws<ArgumentNullException> (() => client.Send (null, message));
				Assert.Throws<ArgumentNullException> (() => client.Send (options, null));

				Assert.Throws<ArgumentNullException> (() => client.Send (message, null, recipients));
				Assert.Throws<ArgumentNullException> (() => client.Send (message, sender, null));
				Assert.Throws<InvalidOperationException> (() => client.Send (message, sender, empty));

				Assert.Throws<ArgumentNullException> (() => client.Send (null, message, sender, recipients));
				Assert.Throws<ArgumentNullException> (() => client.Send (options, null, sender, recipients));
				Assert.Throws<ArgumentNullException> (() => client.Send (options, message, null, recipients));
				Assert.Throws<ArgumentNullException> (() => client.Send (options, message, sender, null));
				Assert.Throws<InvalidOperationException> (() => client.Send (options, message, sender, empty));

				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (null));

				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (null, message));
				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (options, null));

				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (message, null, recipients));
				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (message, sender, null));
				Assert.Throws<InvalidOperationException> (async () => await client.SendAsync (message, sender, empty));

				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (null, message, sender, recipients));
				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (options, null, sender, recipients));
				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (options, message, null, recipients));
				Assert.Throws<ArgumentNullException> (async () => await client.SendAsync (options, message, sender, null));
				Assert.Throws<InvalidOperationException> (async () => await client.SendAsync (options, message, sender, empty));

				// Expand
				Assert.Throws<ArgumentNullException> (() => client.Expand (null));
				Assert.Throws<ArgumentException> (() => client.Expand (string.Empty));
				Assert.Throws<ArgumentException> (() => client.Expand ("line1\r\nline2"));
				Assert.Throws<ArgumentNullException> (async () => await client.ExpandAsync (null));
				Assert.Throws<ArgumentException> (async () => await client.ExpandAsync (string.Empty));
				Assert.Throws<ArgumentException> (async () => await client.ExpandAsync ("line1\r\nline2"));

				// Verify
				Assert.Throws<ArgumentNullException> (() => client.Verify (null));
				Assert.Throws<ArgumentException> (() => client.Verify (string.Empty));
				Assert.Throws<ArgumentException> (() => client.Verify ("line1\r\nline2"));
				Assert.Throws<ArgumentNullException> (async () => await client.VerifyAsync (null));
				Assert.Throws<ArgumentException> (async () => await client.VerifyAsync (string.Empty));
				Assert.Throws<ArgumentException> (async () => await client.VerifyAsync ("line1\r\nline2"));
			}
		}

		static void AssertDefaultValues (string host, int port, SecureSocketOptions options, Uri expected)
		{
			SmtpClient.ComputeDefaultValues (host, ref port, ref options, out Uri uri, out bool starttls);

			if (expected.PathAndQuery == "/?starttls=when-available") {
				Assert.AreEqual (SecureSocketOptions.StartTlsWhenAvailable, options, "{0}", expected);
				Assert.IsTrue (starttls, "{0}", expected);
			} else if (expected.PathAndQuery == "/?starttls=always") {
				Assert.AreEqual (SecureSocketOptions.StartTls, options, "{0}", expected);
				Assert.IsTrue (starttls, "{0}", expected);
			} else if (expected.Scheme == "smtps") {
				Assert.AreEqual (SecureSocketOptions.SslOnConnect, options, "{0}", expected);
				Assert.IsFalse (starttls, "{0}", expected);
			} else {
				Assert.AreEqual (SecureSocketOptions.None, options, "{0}", expected);
				Assert.IsFalse (starttls, "{0}", expected);
			}

			Assert.AreEqual (expected.ToString (), uri.ToString ());
			Assert.AreEqual (expected.Port, port, "{0}", expected);
		}

		[Test]
		public void TestComputeDefaultValues ()
		{
			const string host = "smtp.skyfall.net";

			AssertDefaultValues (host, 0, SecureSocketOptions.None, new Uri ($"smtp://{host}:25"));
			AssertDefaultValues (host, 25, SecureSocketOptions.None, new Uri ($"smtp://{host}:25"));
			AssertDefaultValues (host, 465, SecureSocketOptions.None, new Uri ($"smtp://{host}:465"));

			AssertDefaultValues (host, 0, SecureSocketOptions.SslOnConnect, new Uri ($"smtps://{host}:465"));
			AssertDefaultValues (host, 25, SecureSocketOptions.SslOnConnect, new Uri ($"smtps://{host}:25"));
			AssertDefaultValues (host, 465, SecureSocketOptions.SslOnConnect, new Uri ($"smtps://{host}:465"));

			AssertDefaultValues (host, 0, SecureSocketOptions.StartTls, new Uri ($"smtp://{host}:25/?starttls=always"));
			AssertDefaultValues (host, 25, SecureSocketOptions.StartTls, new Uri ($"smtp://{host}:25/?starttls=always"));
			AssertDefaultValues (host, 465, SecureSocketOptions.StartTls, new Uri ($"smtp://{host}:465/?starttls=always"));

			AssertDefaultValues (host, 0, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"smtp://{host}:25/?starttls=when-available"));
			AssertDefaultValues (host, 25, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"smtp://{host}:25/?starttls=when-available"));
			AssertDefaultValues (host, 465, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"smtp://{host}:465/?starttls=when-available"));

			AssertDefaultValues (host, 0, SecureSocketOptions.Auto, new Uri ($"smtp://{host}:25/?starttls=when-available"));
			AssertDefaultValues (host, 25, SecureSocketOptions.Auto, new Uri ($"smtp://{host}:25/?starttls=when-available"));
			AssertDefaultValues (host, 465, SecureSocketOptions.Auto, new Uri ($"smtps://{host}:465"));
		}

		static Socket Connect (string host, int port)
		{
			var ipAddresses = Dns.GetHostAddresses (host);
			Socket socket = null;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				try {
					socket.Connect (ipAddresses[i], port);
					break;
				} catch {
					socket.Dispose ();
					socket = null;
				}
			}

			return socket;
		}

		[Test]
		public void TestSslHandshakeExceptions ()
		{
			using (var client = new SmtpClient ()) {
				Assert.Throws<SslHandshakeException> (() => client.Connect ("www.gmail.com", 80, true));
				Assert.Throws<SslHandshakeException> (async () => await client.ConnectAsync ("www.gmail.com", 80, true));

				using (var socket = Connect ("www.gmail.com", 80))
					Assert.Throws<SslHandshakeException> (() => client.Connect (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));

				using (var socket = Connect ("www.gmail.com", 80))
					Assert.Throws<SslHandshakeException> (async () => await client.ConnectAsync (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));
			}
		}

		[Test]
		public void TestSyncRoot ()
		{
			using (var client = new SmtpClient ()) {
				Assert.AreEqual (client, client.SyncRoot);
			}
		}

		[Test]
		public void TestSendWithoutSenderOrRecipients ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();

				client.LocalDomain = "127.0.0.1";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				message.From.Clear ();
				message.Sender = null;
				Assert.Throws<InvalidOperationException> (() => client.Send (message));

				message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
				message.To.Clear ();
				Assert.Throws<InvalidOperationException> (() => client.Send (message));

				client.Disconnect (true);
			}
		}

		[Test]
		public async void TestSendWithoutSenderOrRecipientsAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();

				client.LocalDomain = "127.0.0.1";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				message.From.Clear ();
				message.Sender = null;
				Assert.Throws<InvalidOperationException> (async () => await client.SendAsync (message));

				message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
				message.To.Clear ();
				Assert.Throws<InvalidOperationException> (async () => await client.SendAsync (message));

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestInvalidStateExceptions ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();
				var sender = message.From.Mailboxes.FirstOrDefault ();
				var recipients = message.To.Mailboxes.ToList ();
				var options = FormatOptions.Default;

				client.LocalDomain = "127.0.0.1";

				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate ("username", "password"));
				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate (new NetworkCredential ("username", "password")));
				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate (new SaslMechanismPlain ("username", "password")));

				Assert.Throws<ServiceNotConnectedException> (() => client.NoOp ());

				Assert.Throws<ServiceNotConnectedException> (() => client.Send (options, message, sender, recipients));
				Assert.Throws<ServiceNotConnectedException> (() => client.Send (message, sender, recipients));
				Assert.Throws<ServiceNotConnectedException> (() => client.Send (options, message));
				Assert.Throws<ServiceNotConnectedException> (() => client.Send (message));

				Assert.Throws<ServiceNotConnectedException> (() => client.Expand ("user@example.com"));
				Assert.Throws<ServiceNotConnectedException> (() => client.Verify ("user@example.com"));

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.Throws<InvalidOperationException> (() => client.Connect ("host", 465, SecureSocketOptions.SslOnConnect));
				Assert.Throws<InvalidOperationException> (() => client.Connect ("host", 465, true));

				using (var socket = Connect ("www.gmail.com", 80))
					Assert.Throws<InvalidOperationException> (() => client.Connect (socket, "host", 465, SecureSocketOptions.SslOnConnect));

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.Send (options, message, sender, recipients));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.Send (message, sender, recipients));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.Send (options, message));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.Send (message));

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.Throws<InvalidOperationException> (() => client.Authenticate ("username", "password"));
				Assert.Throws<InvalidOperationException> (() => client.Authenticate (new NetworkCredential ("username", "password")));
				Assert.Throws<InvalidOperationException> (() => client.Authenticate (new SaslMechanismPlain ("username", "password")));

				client.Disconnect (true);
			}
		}

		[Test]
		public async void TestInvalidStateExceptionsAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();
				var sender = message.From.Mailboxes.FirstOrDefault ();
				var recipients = message.To.Mailboxes.ToList ();
				var options = FormatOptions.Default;

				client.LocalDomain = "127.0.0.1";

				Assert.Throws<ServiceNotConnectedException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.AuthenticateAsync (new NetworkCredential ("username", "password")));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password")));

				Assert.Throws<ServiceNotConnectedException> (async () => await client.NoOpAsync ());

				Assert.Throws<ServiceNotConnectedException> (async () => await client.SendAsync (options, message, sender, recipients));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.SendAsync (message, sender, recipients));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.SendAsync (options, message));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.SendAsync (message));

				Assert.Throws<ServiceNotConnectedException> (async () => await client.ExpandAsync ("user@example.com"));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.VerifyAsync ("user@example.com"));

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.Throws<InvalidOperationException> (async () => await client.ConnectAsync ("host", 465, SecureSocketOptions.SslOnConnect));
				Assert.Throws<InvalidOperationException> (async () => await client.ConnectAsync ("host", 465, true));

				using (var socket = Connect ("www.gmail.com", 80))
					Assert.Throws<InvalidOperationException> (async () => await client.ConnectAsync (socket, "host", 465, SecureSocketOptions.SslOnConnect));

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.SendAsync (options, message, sender, recipients));
				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.SendAsync (message, sender, recipients));
				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.SendAsync (options, message));
				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.SendAsync (message));

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.Throws<InvalidOperationException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.Throws<InvalidOperationException> (async () => await client.AuthenticateAsync (new NetworkCredential ("username", "password")));
				Assert.Throws<InvalidOperationException> (async () => await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password")));

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestConnectGMail ()
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("smtp.gmail.com", 0, SecureSocketOptions.SslOnConnect);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				client.Disconnect (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
			}
		}

		[Test]
		public async void TestConnectGMailAsync ()
		{
			using (var client = new SmtpClient ()) {
				await client.ConnectAsync ("smtp.gmail.com", 0, SecureSocketOptions.SslOnConnect);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				await client.DisconnectAsync (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
			}
		}

		[Test]
		public void TestConnectGMailSocket ()
		{
			using (var client = new SmtpClient ()) {
				var socket = Connect ("smtp.gmail.com", 465);
				client.Connect (socket, "smtp.gmail.com", 465, SecureSocketOptions.Auto);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				client.Disconnect (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
			}
		}

		[Test]
		public async void TestConnectGMailSocketAsync ()
		{
			using (var client = new SmtpClient ()) {
				var socket = Connect ("smtp.gmail.com", 465);
				await client.ConnectAsync (socket, "smtp.gmail.com", 465, SecureSocketOptions.Auto);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				await client.DisconnectAsync (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
			}
		}

		[Test]
		public void TestConnectYahoo ()
		{
			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new SmtpClient ()) {
					var uri = new Uri ("smtp://smtp.mail.yahoo.com:587/?starttls=always");
					client.Connect (uri, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					client.Disconnect (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				}
			}
		}

		[Test]
		public async void TestConnectYahooAsync ()
		{
			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new SmtpClient ()) {
					var uri = new Uri ("smtp://smtp.mail.yahoo.com:587/?starttls=always");
					await client.ConnectAsync (uri, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					await client.DisconnectAsync (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				}
			}
		}

		[Test]
		public void TestConnectYahooSocket ()
		{
			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new SmtpClient ()) {
					var socket = Connect ("smtp.mail.yahoo.com", 587);
					client.Connect (socket, "smtp.mail.yahoo.com", 587, SecureSocketOptions.StartTls, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					client.Disconnect (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				}
			}
		}

		[Test]
		public async void TestConnectYahooSocketAsync ()
		{
			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new SmtpClient ()) {
					var socket = Connect ("smtp.mail.yahoo.com", 587);
					await client.ConnectAsync (socket, "smtp.mail.yahoo.com", 587, SecureSocketOptions.StartTls, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					await client.DisconnectAsync (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				}
			}
		}

		[Test]
		public void TestBasicFunctionality ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"));
			commands.Add (new SmtpReplayCommand ("VRFY Smith\r\n", "rfc0821-vrfy.txt"));
			commands.Add (new SmtpReplayCommand ("EXPN Example-People\r\n", "rfc0821-expn.txt"));
			commands.Add (new SmtpReplayCommand ("NOOP\r\n", "comcast-noop.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (100000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// disable PLAIN authentication
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Verify ("Smith");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Verify: {0}", ex);
				}

				try {
					client.Expand ("Example-People");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Expand: {0}", ex);
				}

				try {
					client.NoOp ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in NoOp: {0}", ex);
				}

				var message = CreateSimpleMessage ();
				var options = FormatOptions.Default;

				try {
					client.Send (message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Send (message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Send (options, message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Send (options, message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestBasicFunctionalityAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"));
			commands.Add (new SmtpReplayCommand ("VRFY Smith\r\n", "rfc0821-vrfy.txt"));
			commands.Add (new SmtpReplayCommand ("EXPN Example-People\r\n", "rfc0821-expn.txt"));
			commands.Add (new SmtpReplayCommand ("NOOP\r\n", "comcast-noop.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (100000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// disable PLAIN authentication
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.VerifyAsync ("Smith");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Verify: {0}", ex);
				}

				try {
					await client.ExpandAsync ("Example-People");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Expand: {0}", ex);
				}

				try {
					await client.NoOpAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in NoOp: {0}", ex);
				}

				var message = CreateSimpleMessage ();
				var options = FormatOptions.Default;

				try {
					await client.SendAsync (message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.SendAsync (message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.SendAsync (options, message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.SendAsync (options, message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestSaslAuthentication ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (100000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (new Uri ("smtp://localhost"), credentials);

					client.Authenticate (sasl);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestSaslAuthenticationAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (100000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (new Uri ("smtp://localhost"), credentials);

					await client.AuthenticateAsync (sasl);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestEightBitMime ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateEightBitMessage ());
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestEightBitMimeAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.SendAsync (CreateEightBitMessage ());
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		static long Measure (MimeMessage message)
		{
			var options = FormatOptions.Default.Clone ();

			options.NewLineFormat = NewLineFormat.Dos;

			using (var measure = new MeasuringStream ()) {
				message.WriteTo (options, measure);
				return measure.Length;
			}
		}

		[TestCase (false, TestName = "TestBinaryMimeNoProgress")]
		[TestCase (true, TestName = "TestBinaryMimeWithProgress")]
		public void TestBinaryMime (bool showProgress)
		{
			var message = CreateBinaryMessage ();
			var size = Measure (message);
			string bdat;

			using (var memory = new MemoryStream ()) {
				var options = FormatOptions.Default.Clone ();

				options.NewLineFormat = NewLineFormat.Dos;

				var bytes = Encoding.ASCII.GetBytes (string.Format ("BDAT {0} LAST\r\n", size));
				memory.Write (bytes, 0, bytes.Length);
				message.WriteTo (options, memory);

				bytes = memory.GetBuffer ();

				bdat = Encoding.UTF8.GetString (bytes, 0, (int) memory.Length);
			}

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+binarymime.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=BINARYMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand (bdat, "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.BinaryMime), "Failed to detect BINARYMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Chunking), "Failed to detect CHUNKING extension");

				try {
					if (showProgress) {
						var progress = new MyProgress ();

						client.Send (message, progress: progress);

						Assert.AreEqual (size, progress.BytesTransferred, "BytesTransferred");
					} else {
						client.Send (message);
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[TestCase (false, TestName = "TestBinaryMimeAsyncNoProgress")]
		[TestCase (true, TestName = "TestBinaryMimeAsyncWithProgress")]
		public async void TestBinaryMimeAsync (bool showProgress)
		{
			var message = CreateBinaryMessage ();
			var size = Measure (message);
			string bdat;

			using (var memory = new MemoryStream ()) {
				var options = FormatOptions.Default.Clone ();

				options.NewLineFormat = NewLineFormat.Dos;

				var bytes = Encoding.ASCII.GetBytes (string.Format ("BDAT {0} LAST\r\n", size));
				memory.Write (bytes, 0, bytes.Length);
				message.WriteTo (options, memory);

				bytes = memory.GetBuffer ();

				bdat = Encoding.UTF8.GetString (bytes, 0, (int)memory.Length);
			}

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+binarymime.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=BINARYMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand (bdat, "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.BinaryMime), "Failed to detect BINARYMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Chunking), "Failed to detect CHUNKING extension");

				try {
					if (showProgress) {
						var progress = new MyProgress ();

						await client.SendAsync (message, progress: progress);

						Assert.AreEqual (size, progress.BytesTransferred, "BytesTransferred");
					} else {
						await client.SendAsync (message);
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[TestCase (false, TestName = "TestPipeliningNoProgress")]
		[TestCase (true, TestName = "TestPipeliningWithProgress")]
		public void TestPipelining (bool showProgress)
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+pipelining.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\nRCPT TO:<recipient@example.com>\r\n", "pipelined-mail-from-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), "Failed to detect PIPELINING extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					var message = CreateEightBitMessage ();

					if (showProgress) {
						var progress = new MyProgress ();

						client.Send (message, progress: progress);

						Assert.AreEqual (Measure (message), progress.BytesTransferred, "BytesTransferred");
					} else {
						client.Send (message);
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[TestCase (false, TestName = "TestPipeliningAsyncNoProgress")]
		[TestCase (true, TestName = "TestPipeliningAsyncWithProgress")]
		public async void TestPipeliningAsync (bool showProgress)
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+pipelining.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\nRCPT TO:<recipient@example.com>\r\n", "pipelined-mail-from-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), "Failed to detect PIPELINING extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					var message = CreateEightBitMessage ();

					if (showProgress) {
						var progress = new MyProgress ();

						await client.SendAsync (message, progress: progress);

						Assert.AreEqual (Measure (message), progress.BytesTransferred, "BytesTransferred");
					} else {
						await client.SendAsync (message);
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestMailFromMailboxUnavailable ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateSimpleMessage ());
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.SenderNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestMailFromMailboxUnavailableAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.SendAsync (CreateSimpleMessage ());
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.SenderNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestRcptToMailboxUnavailable ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateSimpleMessage ());
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.RecipientNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestRcptToMailboxUnavailableAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.SendAsync (CreateSimpleMessage ());
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.RecipientNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestUnauthorizedAccessException ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Send (CreateSimpleMessage ());
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestUnauthorizedAccessExceptionAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.SendAsync (CreateSimpleMessage ());
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		class DsnSmtpClient : SmtpClient
		{
			protected override string GetEnvelopeId (MimeMessage message)
			{
				var id = base.GetEnvelopeId (message);

				Assert.IsNull (id);

				return message.MessageId;
			}

			protected override DeliveryStatusNotification? GetDeliveryStatusNotifications (MimeMessage message, MailboxAddress mailbox)
			{
				var notify = base.GetDeliveryStatusNotifications (message, mailbox);

				Assert.IsFalse (notify.HasValue);

				return DeliveryStatusNotification.Delay | DeliveryStatusNotification.Failure | DeliveryStatusNotification.Success;
			}
		}

		[Test]
		public void TestDeliveryStatusNotification ()
		{
			var message = CreateEightBitMessage ();
			message.MessageId = MimeUtils.GenerateMessageId ();

			var mailFrom = string.Format ("MAIL FROM:<sender@example.com> BODY=8BITMIME ENVID={0}\r\n", message.MessageId);

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+dsn.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand (mailFrom, "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com> NOTIFY=SUCCESS,FAILURE,DELAY\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new DsnSmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Dsn), "Failed to detect DSN extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), "Failed to detect PIPELINING extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// disable pipelining
				client.Capabilities &= ~SmtpCapabilities.Pipelining;

				try {
					client.Send (message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestDeliveryStatusNotificationAsync ()
		{
			var message = CreateEightBitMessage ();
			message.MessageId = MimeUtils.GenerateMessageId ();

			var mailFrom = string.Format ("MAIL FROM:<sender@example.com> BODY=8BITMIME ENVID={0}\r\n", message.MessageId);

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+dsn.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand (mailFrom, "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com> NOTIFY=SUCCESS,FAILURE,DELAY\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new DsnSmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Dsn), "Failed to detect DSN extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), "Failed to detect PIPELINING extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// disable pipelining
				client.Capabilities &= ~SmtpCapabilities.Pipelining;

				try {
					await client.SendAsync (message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}
	}
}
