using System.Threading;
using AscensionBot.UI;

namespace AscensionBot
{
    class Loader
    {
        static Thread thread;

        static int Load(string args)
        {
            thread = new Thread(App.Main);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return 1;
        }
    }
}
