namespace TeleSharp.TL.TL
{
    public abstract class WebPageAttribute : TLObject
    {
        private TLWebPageAttributeTheme _theme;

        public WebPageAttribute(TLWebPageAttributeTheme theme)
        {
            this._theme = theme;
        }
    }
}