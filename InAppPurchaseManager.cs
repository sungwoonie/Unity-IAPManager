using System;
using System.Collections;
using System.Collections.Generic;
using BackEnd.Functions;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using UnityEngine.Purchasing.Security;

namespace StarCloudgamesLibrary
{
    public class InAppPurchaseManager : SingleTon<InAppPurchaseManager>, IDetailedStoreListener
    {
        public TextAsset productCSV;

        private IStoreController storeController;
        private IExtensionProvider storeExtensionProvider;

        private IAppleExtensions appleExtensions;

        private Dictionary<string, Dictionary<string, string>> parsedProductData;
        private List<InAppPurchaseProduct> productLists;

        private PurchaseState purchaseState;

        #region "Unity"

        protected override void Awake()
        {
            base.Awake();
            InitializeCSV();
        }

        private void Start()
        {
            InitializeIAP();
        }

        #endregion

        #region "Product"

        public bool AlreadyPurchased(string productID)
        {
            var userReceipts = BackendManager.instance.userDatabaseController.UserReceiptData.ReceiptList;
            var targetProduct = productLists.Find(x => x.productID.Equals(productID));

            if(string.IsNullOrEmpty(targetProduct.productID) || userReceipts == null)
            {
                DebugManager.DebugServerWarningMessage($"{productID} is null or userReceipt is null. UserReceipt Count : {userReceipts.Count} Target Product id : {targetProduct.productID}");
                return false;
            }

            foreach(var userReceipt in userReceipts)
            {
                if(userReceipt.productID.Equals(targetProduct.googleID) || userReceipt.productID.Equals(targetProduct.appleID))
                {
                    return true;
                }
            }

            return false;
        }

        private void InitializeCSV()
        {
            productLists = new List<InAppPurchaseProduct>();
            parsedProductData = new Dictionary<string, Dictionary<string, string>>();
            parsedProductData = CSVReader.Read(productCSV);

            foreach(var item in parsedProductData.Values)
            {
                var newProduct = new InAppPurchaseProduct();

                newProduct.productID = item["productID"].ToString();
                newProduct.googleID = item["googleID"].ToString();
                newProduct.appleID = item["appleID"].ToString();
                newProduct.productType = (ProductType)int.Parse(item["productType"].ToString());

                newProduct.reward = new SCReward(item["rewardType"].ToString(), item["rewardID"].ToString(), item["rewardAmount"].ToString(), newProduct.productID);

                productLists.Add(newProduct);
            }
        }

        private bool AddProduct(ConfigurationBuilder builder)
        {
            if(productLists.Count <= 0)
            {
                DebugManager.DebugServerWarningMessage("Product is not exist to Add");
                return false;
            }

            foreach(var product in productLists)
            {
                builder.AddProduct(product.productID, product.productType, new IDs()
                {
                    { product.googleID, GooglePlay.Name},
                    { product.appleID, AppleAppStore.Name}
                });

                DebugManager.DebugServerMessage($"{product.productID} Added. Google ID : {product.googleID}. Apple ID : {product.appleID}");
            }

            return true;
        }

        public string GetPriceInIsoFormat(string productId)
        {
            var product = storeController.products.WithID(productId);

            if(product == null)
            {
                DebugManager.DebugServerErrorMessage($"{productId} is not exist");
                return "Invalid";
            }

            return product.metadata.localizedPrice + " " + product.metadata.isoCurrencyCode;
        }

        #endregion

        #region "Initialize"

        private void InitializeIAP()
        {
            if(Initialized())
            {
                DebugManager.DebugServerWarningMessage("IAP is already initialized. but trying to initialize");
                return;
            }

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

            if(AddProduct(builder))
            {
                UnityPurchasing.Initialize(this, builder);
            }
        }

        public bool Initialized()
        {
            return storeController != null && storeExtensionProvider != null;
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            DebugManager.DebugServerMessage("IAP initialized!");

            storeController = controller;
            storeExtensionProvider = extensions;

            var productList = storeController.products.all;
            foreach (var product in productList)
            {
                if(product.hasReceipt)
                {
                    purchaseState = PurchaseState.Restore;
                }
            }

            appleExtensions = extensions.GetExtension<IAppleExtensions>();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            DebugManager.DebugServerErrorMessage($"IAP Initialize Failed : {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            DebugManager.DebugServerErrorMessage($"IAP Initialize Failed : {error} , {message}");
        }

        #endregion

        #region "Purchase"

        public void Purchase(string productID)
        {
            UIManager.instance.ShowNetworkIndicator(true);

            if (!Initialized())
            {
                DebugManager.DebugServerErrorMessage("IAPManager is not initialized. but trying to purchase");
                UIManager.instance.SystemMessage(true, "PurchaseFailed_NotInitialized", "PurchaseFailed_NotInitialized_Description");

                InitializeIAP();
                UIManager.instance.ShowNetworkIndicator(false);
                return;
            }

            var product = storeController.products.WithID(productID);

            if(product == null || !product.availableToPurchase)
            {
                DebugManager.DebugServerErrorMessage($"{productID} is not exist or can't buy");
                UIManager.instance.SystemMessage(true, "PurchaseFailed_NotExistProduct", "PurchaseFailed_NotExistProduct_Description");
                return;
            }

            purchaseState = PurchaseState.Purchase;

#if UNITY_EDITOR
            TestPurchase(productID);
#else
            storeController.InitiatePurchase(productID);
#endif
        }

        private void TestPurchase(string productID)
        {
            DebugManager.DebugServerMessage($"Test purchase start. productID : {productID}");

            List<SCReward> rewards = new List<SCReward>();

            var newReward = productLists.Find(x => x.productID == productID).reward;
            if(newReward != null)
            {
                rewards.Add(newReward);
                DebugManager.DebugServerMessage($"Test reward added. rewardName : {newReward.rewardName}");
            }

            ServerReceipt testReceipt = new ServerReceipt
            {
                productID = productID + " test",
                purchaseDate = DateTime.UtcNow.ToString(),
                transactionID = productID
            };

            BackendManager.instance.userDatabaseController.UserReceiptData.ReceiptList.Add(testReceipt);

            if(RewardManager.instance.GiveRewards(rewards, true))
            {
                UIManager.instance.SystemMessage(true, "PurchaseSuccess", "PurchaseSuccess_Description");
            }

            UIManager.instance.ShowNetworkIndicator(false);
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            DebugManager.DebugServerErrorMessage($"Purchase {product} Failed : {failureReason}");

            if(failureReason != PurchaseFailureReason.UserCancelled)
            {
                UIManager.instance.SystemMessage(true, $"PurchaseFailed_{failureReason}", $"PurchaseFailed_{failureReason}_Description");
            }

            UIManager.instance.ShowNetworkIndicator(false);
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            DebugManager.DebugServerErrorMessage($"Purchase {product} Failed : {failureDescription}");

            if(failureDescription.reason != PurchaseFailureReason.UserCancelled)
            {
                UIManager.instance.SystemMessage(true, $"PurchaseFailed_{failureDescription.reason}", $"PurchaseFailed_{failureDescription.reason}_Description");
            }

            UIManager.instance.ShowNetworkIndicator(false);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            DebugManager.DebugServerMessage($"Start {(purchaseState == PurchaseState.Purchase ? "Purchase" : "Restore")} {purchaseEvent.purchasedProduct.definition.id}!");

            if (ReceiptValidated(purchaseEvent, out List<ServerReceipt> receipts))
            {
                DebugManager.DebugServerMessage("Valid receipt! start give reward");

                var rewards = new List<SCReward>();

                foreach(ServerReceipt receipt in receipts)
                {
                    var newReward = productLists.Find(x => x.googleID == receipt.productID || x.appleID == receipt.productID).reward;
                    if(newReward != null)
                    {
                        rewards.Add(newReward);
                        DebugManager.DebugServerMessage($"new reward. {newReward.rewardName}, {newReward.rewardID}, {newReward.rewardType}, {newReward.amount}");

                        if(purchaseState == PurchaseState.Purchase)
                        {
                            BackendManager.instance.userDatabaseController.UserReceiptData.ReceiptList.Add(receipt);
                        }
                    }
                    else
                    {
                        DebugManager.DebugServerWarningMessage($"reward is not exist. {receipt.productID}");
                    }
                }

                StartCoroutine(GiveReward(rewards));
                return PurchaseProcessingResult.Complete;
            }
            else
            {
                DebugManager.DebugInGameErrorMessage($"Purchase failed. Invalid receipt");
                UIManager.instance.SystemMessage(true, $"PurchaseFailed_InvalidReceipt", $"PurchaseFailed_InvalidReceipt_Description");
            }

            UIManager.instance.ShowNetworkIndicator(false);
            return PurchaseProcessingResult.Complete;
        }

        private IEnumerator GiveReward(List<SCReward> rewards)
        {
            yield return new WaitForEndOfFrame();

            DebugManager.DebugServerMessage($"Start to give IAP Reward. reward count : {rewards.Count}");

            if(purchaseState == PurchaseState.Purchase)
            {
                if(RewardManager.instance.GiveRewards(rewards, purchaseState == PurchaseState.Purchase))
                {
                    UIManager.instance.SystemMessage(true, "PurchaseSuccess", "PurchaseSuccess_Description");
                }
            }

            DebugManager.DebugServerMessage($"Purchase Finished!");

            UIManager.instance.ShowNetworkIndicator(false);
        }

#endregion

        #region "Receipt"

        private bool ReceiptValidated(PurchaseEventArgs purchaseEvent, out List<ServerReceipt> receipts)
        {
            DebugManager.DebugServerMessage("Start Validate Receipt");

            var valid = true;
            var validator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), Application.identifier);

            receipts = new List<ServerReceipt>();

            try
            {
                var result = validator.Validate(purchaseEvent.purchasedProduct.receipt);

                foreach(IPurchaseReceipt purchaseReceipt in result)
                {
                    var newReceipt = new ServerReceipt
                    {
                        productID = purchaseReceipt.productID,
                        purchaseDate = purchaseReceipt.purchaseDate.ToString(),
                        transactionID = purchaseReceipt.transactionID
                    };

                    receipts.Add(newReceipt);
                    DebugManager.DebugServerMessage($"Added new receipt. {newReceipt.productID}, {newReceipt.purchaseDate}, {newReceipt.transactionID}");
                }
            }
            catch(IAPSecurityException)
            {
                DebugManager.DebugServerErrorMessage("Invalid receipt!");
                valid = false;
            }

            return valid;
        }

        #endregion

        #region "Restore"

        public void Restore()
        {
            UIManager.instance.ShowNetworkIndicator(true);
            purchaseState = PurchaseState.Restore;
            appleExtensions.RestoreTransactions(RestoreCompleted);
        }

        private void RestoreCompleted(bool success, string error)
        {
            UIManager.instance.ShowNetworkIndicator(false);

            if(success)
            {
                UIManager.instance.SystemMessage(true, "RestoreSuccess", "RestoreSuccess_Description");
            }
            else
            {
                UIManager.instance.SystemMessage(true, "RestoreFailed", "RestoreFailed_Description");
                DebugManager.DebugServerErrorMessage($"Restorefailed. error : {error}");
            }
        }

        #endregion
    }

    public enum PurchaseState
    {
        Purchase, Restore
    }
}