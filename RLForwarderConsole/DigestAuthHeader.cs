﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RLForwarderConsole
{
    internal class DigestAuthHeader
    {
        public DigestAuthHeader(string realm, string username, string password, string nonce, string qualityOfProtection,
            int nonceCount, string clientNonce, string opaque) //string opaque)
        {
            Realm = realm;
            Username = username;
            Password = password;
            Nonce = nonce;
            QualityOfProtection = qualityOfProtection;
            NonceCount = nonceCount;
            ClientNonce = clientNonce;
            Opaque = opaque;
            Opaque = opaque;
        }

        public string Realm { get; }
        public string Username { get; }
        public string Password { get; }
        public string Nonce { get; }
        public string QualityOfProtection { get; }
        public int NonceCount { get; }
        public string ClientNonce { get; }
        public string Opaque { get; }
    }
}
