namespace TeleSharp.TL.Payments
{
    public abstract class PaymentSavedCredentials : TLObject
    {
        private TLPaymentSavedCredentialsCard _paymentSavedCredentialsCard;

        public PaymentSavedCredentials(TLPaymentSavedCredentialsCard paymentSavedCredentialsCard)
        {
            this._paymentSavedCredentialsCard = paymentSavedCredentialsCard;
        }
    }
}