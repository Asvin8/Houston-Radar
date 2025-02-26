<?php

use Firebase\JWT\JWT;
use Firebase\JWT\Key;

include_once("Traffic_Data/config.inc.php");
include_once("Traffic_Data/td.inc.php");
include_once("Traffic_Data/ac.inc.php");

//include_once(SMARTY_LIB . '/Smarty.class.php');

header("Content-Security-Policy: script-src 'self' http://api.spectrumtraffic.com");
header("Access-Control-Allow-Origin: *");
header("access-control-allow-credentials: true");
header("access-control-allow-headers: content-type");
header("access-control-allow-methods: GET,HEAD,PUT,PATCH,POST,DELETE");
header("access-control-allow-origin: http://localhost:3001");

//$_POST = json_decode(file_get_contents("php://input"), true);

//Check Which Action To Perform:
if (isset($_POST['act'])) { $act = trim($_POST['act']); }
elseif (isset($_GET['act'])) { $act = trim($_GET['act']); }
else { $act = null; }

function isValidIPv4($ip) { return filter_var($ip, FILTER_VALIDATE_IP, FILTER_FLAG_IPV4) !== false; }

function errorOccured($errorMessage) {
  $resultant['err'] = 1;
  $resultant['message'] = $errorMessage;
  echo json_encode($resultant);
  exit(0);
}

$err = null;

switch ($act) {

  case "get_schedules":

    $resultant['err'] = 0;
    $ip_address = is_null($_GET['ip_address']) ? null : trim($_GET['ip_address']);

    if (!isValidIPv4($ip_address)) {
      errorOccured("Invalid ip address format");
    }

    $start = new DateTime("2024-05-07 00:00:00");
    $end = new DateTime("2024-05-07 23:59:59");

    if (!is_null($ip_address)) {
      $schedules[0]['start'] = $start->format('U');
      $schedules[0]['end'] = $end->format('U');
      $resultant['schedules'] = $schedules;
    } else {
      $resultant['err'] = 1;
      $resultant['message'] = "Invalid IP address";
    }

    echo json_encode($resultant);
    exit(0);

  case "upload_gzip":
    header("Content-Type: application/json");

    // Check if file was uploaded
    if (!isset($_FILES['file']) || $_FILES['file']['error'] !== 0) {
        errorOccured("No valid file was uploaded.");
    }

    // Validate GZIP file type
    if ($_FILES['file']['type'] !== "application/gzip") {
        errorOccured("Invalid file type: " . $_FILES['file']['type']);
    }

    // Read and decompress GZIP data in memory
    $gzipFilePath = $_FILES['file']['tmp_name'];
    $compressedData = file_get_contents($gzipFilePath);

    if ($compressedData === false) {
        errorOccured("Failed to read uploaded GZIP file.");
    }

    // Decompress GZIP data
    $jsonContent = gzdecode($compressedData);

    if ($jsonContent === false) {
        errorOccured("Failed to decompress GZIP file.");
    }

    // Decode JSON
    $decodedJson = json_decode($jsonContent, true);
    if ($decodedJson === null) {
        errorOccured("Invalid JSON format in GZIP file.");
    }

    // Inject array into database.
    //jsonTrace($decodedJson['vehicles']);
    debugTrace(-1);


    foreach($decodedJson['vehicles'] as $vehicle){
        //jsonTrace($vehicle);

        if(!addGenericDB($vehicle, 'td_speedlane_data', 'id', $msg, $err)){
           debugTrace("$msg: $err");
        }

    }


    //jsonTrace($decodedJson, 1);

    // Return JSON response
    echo json_encode([
        "err" => 0,
        "message" => "File uploaded and processed successfully",
        "json_contents" => $decodedJson
    ], JSON_PRETTY_PRINT);
    exit(0);

  default:
    errorOccured("Unknown method");
}

?>
