#region " Using statements "

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

#endregion

public class RollaballPlayer : MonoBehaviour
{
    #region " Variables definitions "

    public GameObject pickUps;
    public GameObject ground;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI gameEndText;
    public GameObject fireWorks;
    bool gameEnd = false;
    int score = 0;
    float speed = 10;
    float timer = 0;
    Rigidbody PlayerRB;

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        score = 0;
        SetScoreText();
    }

    // Update is called once per frame
    void Update()
    {
        // If Player ball dropped off the play ground, finish game
        if (transform.position.y < -100 && gameEnd == false)
        {
            gameEndText.text = "Oops...\nBetter luck next time!";
            gameEndText.gameObject.SetActive(true);
            gameEnd = true;
        }

        // If game ends, rotate the game end message & eventually fireworks for 9 seconds
        if (gameEnd == true)
        {
            gameEndText.transform.Rotate(new Vector3(0, -15, 0) * speed * Time.deltaTime, Space.Self);
            timer += Time.deltaTime;

            // Reset game after 9 seconds
            if (timer > 9)
            {
                gameEndText.gameObject.SetActive(false);
                fireWorks.SetActive(false);
                gameEnd = false;
                score = 0;
                timer = 0;
                SetScoreText();
                PlayerRB = GetComponent<Rigidbody>();
                PlayerRB.velocity = Vector3.zero;
                transform.localScale = new Vector3(1, 1, 1);
                transform.localPosition = ground.transform.localPosition + new Vector3(0, 4, 0);

                // Reactivate all PickUps
                foreach (Transform pickUp in pickUps.transform)
                {
                    pickUp.gameObject.SetActive(true);
                }
            }
        }
    }

    // Hide the PickUps after triggered by the Player ball, increase Player ball size
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("PickUp"))
        {
            other.gameObject.SetActive(false);

            // Increase Player ball size
            Vector3 scale = transform.localScale;
            scale.x *= 1.1f;
            scale.y *= 1.1f;
            scale.z *= 1.1f;
            transform.localScale = scale;

            score += 1;
            SetScoreText();
        }
    }

    // Display the PickUp trigger count
    void SetScoreText()
    {
        scoreText.text = "Score: " + score.ToString();

        if (score > 11)
        {
            // Display the game end message & fireworks
            gameEndText.text = "You Win!";
            gameEndText.gameObject.SetActive(true);
            fireWorks.SetActive(true);
            gameEnd = true;
        }
    }
}
