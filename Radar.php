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

function errorOccored($errorMessage) {
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
      $resultant['err'] = 1;
      $resultant['message'] = "Invalid IP address format";
      echo json_encode($resultant);
      exit(0);
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
    // Debugging log: Ensure PHP script is executed
    error_log("upload_gzip API called");

    // Check if file was uploaded
    if (!isset($_FILES['file'])) { 
        error_log("No file was sent to API"); 
        errorOccured("No file was sent to API");
    }

    // Only accept gzip file
    $fileType = $_FILES['file']['type'];
    if ($fileType !== "application/gzip") { 
        error_log("Invalid file type: " . $fileType); 
        errorOccured("Invalid file type");
    }

    // Save uploaded file
    $uploadDir = __DIR__ . "/uploads/";
    if (!is_dir($uploadDir)) { mkdir($uploadDir, 0777, true); }

    $uploadedFilePath = $uploadDir . basename($_FILES['file']['name']);
    if (!move_uploaded_file($_FILES['file']['tmp_name'], $uploadedFilePath)) { 
        error_log("File failed to upload"); 
        errorOccured("File failed to upload");
    }

    // Decompress file
    $gzFile = gzopen($uploadedFilePath, 'rb');
    if (!$gzFile) { 
        error_log("Failed to open gz file"); 
        errorOccured("Failed to open gz file");
    }

    // Read and decompress GZIP file
    $jsonContent = "";
    while (!gzeof($gzFile)) {
        $jsonContent .= gzread($gzFile, 4096);
    }
    gzclose($gzFile);

    // Decode JSON
    $decodedJson = json_decode($jsonContent, true);
    if ($decodedJson === null) { 
        error_log("Invalid JSON format in GZIP file"); 
        errorOccured("Invalid JSON format in GZIP file");
    }

    // Debug: Log successful processing
    error_log("File uploaded and processed successfully");

    // Return JSON response
    header("Content-Type: application/json");
    echo json_encode([
        "err" => 0,
        "message" => "File uploaded and decompressed successfully",
        "file_path" => $uploadedFilePath,
        "json_contents" => $decodedJson
    ], JSON_PRETTY_PRINT);
    exit(0);



  default:

    $resultant['err'] = 1;
    $resultant['errors'] = 'Unknown method';
    echo json_encode($resultant);
    exit(0);
}

?>
