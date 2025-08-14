using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace StarCloudgamesLibrary
{
    [Serializable]
    public struct InAppPurchaseProduct
    {
        public string productID;
        public string googleID;
        public string appleID;
        public ProductType productType;
        public SCReward reward;
    }
}