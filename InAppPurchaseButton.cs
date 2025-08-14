using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace StarCloudgamesLibrary
{
    public class InAppPurchaseButton : MonoBehaviour
    {
        public string productID;
        public TMP_Text priceText;

        #region "Unity"

        private void Start()
        {
            SetPriceText();
        }

        #endregion

        #region "Text"

        private void SetPriceText()
        {
            priceText.text = InAppPurchaseManager.instance.GetPriceInIsoFormat(productID);
        }

        #endregion

        #region "OnClick"

        public void OnClickPurchase()
        {
            InAppPurchaseManager.instance.Purchase(productID);
        }

        #endregion
    }
}