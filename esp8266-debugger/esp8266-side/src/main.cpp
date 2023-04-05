#include <Arduino.h>
#include <ESP8266WiFi.h>
#include <ESP8266mDNS.h>
#include <WebSocketsServer.h>
// #include <ESP8266WiFiMulti.h>

// ESP8266WiFiMulti wifiMulti;
WebSocketsServer webSocket(81); // Define the socket

// For the AP that will be created
const char *ssid = "ESP8266 debugger";
const char *password = "edrobotics";

IPAddress local_IP(192, 168, 1, 5);
IPAddress gateway(192, 168, 1, 1);
IPAddress subnet(255, 255, 255, 0);

const char* mdnsName = "esp-debugger";


void yeildingDelay(long delayTime)
{
  for (int i=0; i<delayTime/50; ++i)
  {
    yield();
    delay(50);
  }
}

void startWiFi()
{
  WiFi.softAPConfig(local_IP, gateway, subnet);
  WiFi.softAP(ssid, password);

  while (WiFi.softAPgetStationNum() < 1) {delay(100);}

}

void webSocketEvent(uint8_t num, WStype_t type, uint8_t * payload, size_t length)
{
  switch (type)
  {
    case WStype_DISCONNECTED:
      break;
    
    case WStype_CONNECTED:
      webSocket.sendTXT(num, "[INFO] Server-client connection established");
      // webSocket.broadcastTXT("Server connected to client");
      break;
    
    case WStype_TEXT:
      // Do nothing, as the server is only sending
      break;

    case WStype_BIN:
      // Do nothing, as the server is only sending
      break;
  }
}

void startWebSocket()
{
  webSocket.begin();
  webSocket.onEvent(webSocketEvent);
}


void startMDNS()
{
  if (!MDNS.begin(mdnsName))
  {
    // Serial.println("Error setting up MDNS responder");
  }
  // else Serial.println("Started mdns responder");
}


void sendSocketData(String data)
{

}

void setup() {
  Serial.begin(115200);
  Serial.println("");

  startWiFi();
  // Serial.println("WiFi began...");

  startWebSocket();
  // Serial.println("Started websocket");

  startMDNS();
  // Serial.println(WiFi.softAPIP());

}

void loop() {
  webSocket.loop();
  MDNS.update();
  if (Serial.available() > 0)
  {
    String readString;
    readString = Serial.readStringUntil('\n');
    webSocket.broadcastTXT(readString);
  }
  
}