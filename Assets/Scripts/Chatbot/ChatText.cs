using UnityEngine;
using TMPro;

public class ChatText : MonoBehaviour
{
    public TMP_Text chatText;
    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer > 2f)
        {
            chatText.text += "\n🧠 " + System.DateTime.Now.ToLongTimeString();
            timer = 0f;
        }
    }
}
