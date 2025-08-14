using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarCloudgamesLibrary
{
    [Serializable]
    public struct ServerReceipt
    {
        public string productID;
        public string purchaseDate;
        public string transactionID;
    }
}