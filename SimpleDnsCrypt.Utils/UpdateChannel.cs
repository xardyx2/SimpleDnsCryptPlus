namespace SimpleDnsCrypt.Utils
{
    public static class UpdateChannel
    {
        /// <summary>
        /// The only key whose signatures this build will accept as an update. It is the public half
        /// of the release key, also committed at tools/keys/update.pub, and a test asserts the two
        /// stay identical so what ships cannot drift from what is published.
        ///
        /// This is deliberately NOT Christian Hermann's key
        /// (RWTSM+4BNNvkZPNkHgE88ETlhWa+0HDzU5CN8TvbyvmhVUcr6aQXfssV), which is what upstream 0.7.x
        /// trusts. Reusing it would mean accepting whatever the original author signs.
        /// </summary>
        public const string TrustedPublicKey = "RWSdjGjnJcA0sYr6ERSWHtJ7xxcoJqSYt/XSguCeazq9/oaz7NIFLPVF";
    }
}
