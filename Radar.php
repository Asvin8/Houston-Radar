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

// display errors
// ini_set('display_errors', 1);
// ini_set('display_startup_errors', 1);
// error_reporting(E_ALL);


//$_POST = json_decode(file_get_contents("php://input"), true);

//Check Which Action To Perform:
if (isset($_POST['act'])) { $act = trim($_POST['act']); }
elseif (isset($_GET['act'])) { $act = trim($_GET['act']); }
else { $act = null; }

function isValidIPv4($ip) { return filter_var($ip, FILTER_VALIDATE_IP, FILTER_FLAG_IPV4) !== false; }

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

      // check if file was uploaded
      if(!isset($_FILES['files'])) {
        $resultant['err'] = 1;
        $resultant['message'] = "no file was sent to API";
        echo json_encode($resultant);
        exit(0);
      }

      // only accept gzip file
      $fileType = $_FILES['file']['type'];
      if($fileType !== "application/gzip") {
        $resultant['err'] = 1;
        $resultant['message'] = "Invalid file type";
        echo json_encode($resultant);
        exit(0);
      }

      // save uploaded file
      $uploadDir = __DIR__ . "/uploads/";
      if(!is_dir($uploadDir)) { mkdir($uploadDir, 0777, true); }

      // check if file upload failed
      $uploadedFilePath = $uploadDir . basename($_FILES['file']['name']);
      if(!move_uploaded_file($_FILES['file']['tmp_name'], $uploadedFilePath)) {
        $resultant['err'] = 1;
        $resultant['message'] = "File failed to upload";
        echo json_encode($resultant);
        exit(0);
      }

      // decompress file
      $jsonFilePath = str_replace(".gz", "", $uploadedFilePath);
      $gzFile = gzopen($uploadedFilepath, 'rb');
      if(!$gzFile) {
        $resultant['err'] = 1;
        $resultant['message'] = "Failed to open gz file";
        echo json_encode($resultant);
        exit(0);
      }

      // parse gz zip file into JSON
      $jsonContent = "";
      while(!gzeof(gzFile)) { $jsonContent .= gzread($gzFile, 4096);  }
      gzclose($gzFile);

      // print JSON info
      $resultant['err'] = 0;
      $resultant['message'] = "File uploaded and decompressed successfully";
      $resultant['file_path'] = $uploadedFilePath;
      $resultant['json_contents'] = json_decode($jsonContent, true);
      echo json_encode($resultant, JSON_PRETTY_PRINT);
      exit(0);
      

  default:

    $resultant['err'] = 1;
    $resultant['errors'] = 'Unknown method';
    echo json_encode($resultant);
    exit(0);
}
>
