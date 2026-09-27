using UnityEngine;
using UnityEngine.SceneManagement;
namespace Yibi.World
{
    public sealed class ReturnToValley:MonoBehaviour
    {
        public void Return(){SceneManager.LoadScene("Valley");}
    }
}
