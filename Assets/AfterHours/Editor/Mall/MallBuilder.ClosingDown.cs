using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>Homestyle never reopened: CLOSING DOWN banners across its shutter, sale posters, a notice from the administrators.</summary>
    public static partial class MallBuilder
    {
        static void ClosingDownFront(Transform g, WingId w, float y, float u0)
        {
            Vector3 fwd = MallLayout.WingForward(w);
            Quaternion toMall = Quaternion.LookRotation(-fwd);
            Vector3 P(float v, float h, float out_) => MallLayout.WingPoint(w, u0 + 0.15f - 0.06f - out_, v, y + h);
            Material banner = ArtLibrary.Plain("Closing_Banner", new Color(0.95f, 0.85f, 0.1f), 0.3f);
            Material red = ArtLibrary.Plain("Sale_Red", new Color(0.75f, 0.06f, 0.05f), 0.3f);
            Detail("Closing down banner", g, P(0f, 3.0f, 0.005f), new Vector3(9.6f, 0.8f, 0.01f), banner, toMall);
            Sign(g, "CLOSING DOWN  -  EVERYTHING MUST GO", P(0f, 3.0f, 0.012f), -fwd, 9f, 0.06f, false);
            float[] spots = { -3.8f, -1.3f, 1.3f, 3.8f };
            for (int k = 0; k < spots.Length; k++)
            {
                Detail("Sale poster", g, P(spots[k], 1.6f, 0.006f), new Vector3(0.9f, 1.2f, 0.005f), k % 2 == 0 ? red : banner, toMall * Quaternion.Euler(0f, 0f, (k - 1.5f) * 2f));
                Sign(g, k % 2 == 0 ? "70%\nOFF" : "FINAL\nDAYS", P(spots[k], 1.65f, 0.01f), -fwd, 0.8f, 0.07f, false);
            }
            Detail("Administrators' notice", g, P(5.4f, 1.5f, 0.1f), new Vector3(0.3f, 0.42f, 0.003f), whiteFixture, toMall);
            Sign(g, "NOTICE\n\nTHIS STORE IS NOW\nCLOSED. ALL ENQUIRIES\nTO THE JOINT\nADMINISTRATORS.\n\nTHANK YOU FOR\n47 YEARS.", P(5.4f, 1.5f, 0.103f), -fwd, 0.27f, 0.0065f, false);
        }
    }
}
