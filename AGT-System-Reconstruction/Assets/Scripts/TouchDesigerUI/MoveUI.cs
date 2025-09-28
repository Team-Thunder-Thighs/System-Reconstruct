using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class MoveUI : MonoBehaviour
{
    [SerializeField] private UITest uiPosition;
    private RectTransform rect;
    public bool movingHands = false;
    private void Start()
    {
        rect = GetComponent<RectTransform>();
        uiPosition = GetComponent<UITest>();
    }

    private void Update()
    {
        if(uiPosition != null)
        {
            if(!movingHands)
                rect.anchoredPosition = new Vector2((uiPosition.GetCenterX() * Screen.currentResolution.width)/2, (uiPosition.GetCenterY() * Screen.currentResolution.height)/2);
            if(movingHands)
                rect.anchoredPosition = new Vector2((uiPosition.GetCenterX() * Screen.currentResolution.width) - (Screen.currentResolution.width/2), (uiPosition.GetCenterY() * Screen.currentResolution.height) - (Screen.currentResolution.height/2));
        }
    }
}
