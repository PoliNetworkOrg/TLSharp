namespace TeleSharp.TL.Contacts
{
    public abstract class SavedContact : TLObject
    {
        private TLSavedPhoneContact _savedPhoneContact;

        public SavedContact(TLSavedPhoneContact savedPhoneContact)
        {
            this._savedPhoneContact = savedPhoneContact;
        }
    }
}