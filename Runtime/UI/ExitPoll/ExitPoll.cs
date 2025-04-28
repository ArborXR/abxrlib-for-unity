using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExitPoll : MonoBehaviour
{
    public static ExitPoll Instance { get; private set; }
    
    public Button submitButton;
    public Button thumbsUpButton;
    public Button thumbsDownButton;
    public Button oneRatingButton;
    public Button twoRatingButton;
    public Button threeRatingButton;
    public Button fourRatingButton;
    public Button fiveRatingButton;
    public TMP_Text prompt;
    
    public event EventHandler OnTextSubmitted = delegate { };
    public event EventHandler OnThumbsDown = delegate { };
    public event EventHandler OnThumbsUp = delegate { };
    public event EventHandler<RatingEventArgs> OnRating = delegate { };

    private void Awake()
    {
        Instance = this;
    }
    
    private void Start()
    {
        submitButton?.onClick.AddListener(OnSubmitClick);
        thumbsUpButton?.onClick.AddListener(OnThumbsUpClick);
        thumbsDownButton?.onClick.AddListener(OnThumbsDownClick);
        oneRatingButton?.onClick.AddListener(OnOneRatingClick);
        twoRatingButton?.onClick.AddListener(OnTwoRatingClick);
        threeRatingButton?.onClick.AddListener(OnThreeRatingClick);
        fourRatingButton?.onClick.AddListener(OnFourRatingClick);
        fiveRatingButton?.onClick.AddListener(OnFiveRatingClick);
    }
    
    private void OnSubmitClick()
    {
        OnTextSubmitted?.Invoke(this, EventArgs.Empty);
        gameObject.SetActive(false);
    }
    
    private void OnThumbsUpClick()
    {
        OnThumbsUp?.Invoke(this, EventArgs.Empty);
        gameObject.SetActive(false);
    }
    
    private void OnThumbsDownClick()
    {
        OnThumbsDown?.Invoke(this, EventArgs.Empty);
        gameObject.SetActive(false);
    }

    private void OnOneRatingClick()
    {
        OnRating?.Invoke(this, new RatingEventArgs(1));
        gameObject.SetActive(false);
    }
    
    private void OnTwoRatingClick()
    {
        OnRating?.Invoke(this, new RatingEventArgs(2));
        gameObject.SetActive(false);
    }
    
    private void OnThreeRatingClick()
    {
        OnRating?.Invoke(this, new RatingEventArgs(3));
        gameObject.SetActive(false);
    }
    
    private void OnFourRatingClick()
    {
        OnRating?.Invoke(this, new RatingEventArgs(4));
        gameObject.SetActive(false);
    }
    
    private void OnFiveRatingClick()
    {
        OnRating?.Invoke(this, new RatingEventArgs(5));
        gameObject.SetActive(false);
    }
    
    public class RatingEventArgs : EventArgs
    {
        public int rating { get; }

        public RatingEventArgs(int rating)
        {
            this.rating = rating;
        }
    }
}